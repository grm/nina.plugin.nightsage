using NINA.Plugin.NightSage.Infrastructure;
using NINA.Plugin.NightSage.Models;
using System.Collections;
using System.Reflection;

namespace NINA.Plugin.NightSage.Services;

public interface ITargetSchedulerAdapter {
    string Status { get; }
    bool CanWrite { get; }
    IReadOnlyList<ExistingTargetInfo> GetExistingTargets(string profileId);
    Task<TargetSchedulerCreateResult> CreateAsync(string profileId, ImagingPlan plan, bool activateProject, CancellationToken cancellationToken);
}

public sealed class TargetSchedulerReflectionAdapter : ITargetSchedulerAdapter {
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    private Assembly? Assembly => AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => string.Equals(a.GetName().Name, "NINA.Plugin.TargetScheduler", StringComparison.OrdinalIgnoreCase));

    public bool CanWrite {
        get {
            var a = Assembly;
            return a != null && IsCompatible(a.GetName().Version);
        }
    }

    public string Status {
        get {
            var a = Assembly;
            if (a == null) return "Target Scheduler not loaded";
            var version = a.GetName().Version;
            if (!IsCompatible(version)) return $"Target Scheduler {version} detected — NightSage alpha write adapter supports 5.9.x";
            return $"Target Scheduler {version} ready";
        }
    }

    public IReadOnlyList<ExistingTargetInfo> GetExistingTargets(string profileId) {
        var result = new List<ExistingTargetInfo>();
        var a = Assembly;
        if (a == null || !IsCompatible(a.GetName().Version)) return result;

        object? context = null;
        try {
            var interaction = CreateInteraction(a);
            context = ReflectionUtil.Invoke(interaction, "GetContext");
            if (context == null) return result;
            var projects = ReflectionUtil.AsObjects(ReflectionUtil.Invoke(context, "GetAllProjects", profileId));
            foreach (var project in projects) {
                var projectName = ReflectionUtil.Get<string>(project, "Name") ?? "";
                var state = ReflectionUtil.Get(project, "State")?.ToString() ?? "";
                foreach (var target in ReflectionUtil.AsObjects(ReflectionUtil.Get(project, "Targets"))) {
                    result.Add(new ExistingTargetInfo {
                        ProjectName = projectName,
                        TargetName = ReflectionUtil.Get<string>(target, "Name") ?? "",
                        PercentComplete = ReflectionUtil.Get<double>(target, "PercentComplete"),
                        ProjectActive = string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase)
                    });
                }
            }
        } catch {
            return Array.Empty<ExistingTargetInfo>();
        } finally {
            (context as IDisposable)?.Dispose();
        }
        return result;
    }

    public async Task<TargetSchedulerCreateResult> CreateAsync(
        string profileId,
        ImagingPlan plan,
        bool activateProject,
        CancellationToken cancellationToken) {

        if (!plan.IsValidated) throw new InvalidOperationException("NightSage will not write an unvalidated plan.");
        if (plan.Exposures.Count == 0) throw new InvalidOperationException("The plan has no exposures.");
        if (plan.Exposures.Any(x => string.Equals(x.Filter, "OSC", StringComparison.OrdinalIgnoreCase))) {
            throw new InvalidOperationException("Target Scheduler requires every Exposure Template to reference a N.I.N.A. filter. Add a dummy filter (for example 'OSC' or 'LP') to this N.I.N.A. profile, then analyze again.");
        }

        var a = Assembly ?? throw new InvalidOperationException("Target Scheduler is not loaded.");
        if (!IsCompatible(a.GetName().Version)) throw new InvalidOperationException(Status);

        await WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            cancellationToken.ThrowIfCancellationRequested();
            BackupDatabase(a);

            var existing = GetExistingTargets(profileId);
            if (existing.Any(x => EquivalentName(x.TargetName, plan.TargetName))) {
                return new TargetSchedulerCreateResult {
                    Success = false,
                    Message = $"Target '{plan.TargetName}' already exists in Target Scheduler for the active profile. NightSage will not create a duplicate."
                };
            }

            object? context = null;
            var createdTemplates = new List<object>();
            try {
                var interaction = CreateInteraction(a);
                context = ReflectionUtil.Invoke(interaction, "GetContext")
                          ?? throw new InvalidOperationException("Target Scheduler returned no database context.");

                var schemaNs = "NINA.Plugin.TargetScheduler.Database.Schema.";
                var projectType = a.GetType(schemaNs + "Project", true)!;
                var targetType = a.GetType(schemaNs + "Target", true)!;
                var exposurePlanType = a.GetType(schemaNs + "ExposurePlan", true)!;
                var exposureTemplateType = a.GetType(schemaNs + "ExposureTemplate", true)!;

                var templates = ReflectionUtil.AsObjects(ReflectionUtil.Invoke(context, "GetExposureTemplates", profileId)).ToList();
                var templateByExposure = new Dictionary<ExposureRecommendation, object>();

                foreach (var exp in plan.Exposures) {
                    cancellationToken.ThrowIfCancellationRequested();
                    var template = FindMatchingTemplate(templates, exp);
                    if (template == null) {
                        var name = BuildTemplateName(exp);
                        template = Activator.CreateInstance(exposureTemplateType, profileId, name, exp.Filter)
                                   ?? throw new InvalidOperationException("Could not instantiate a Target Scheduler ExposureTemplate.");
                        PopulateExposureTemplate(template, exp);
                        var saved = ReflectionUtil.Invoke(context, "SaveExposureTemplate", template);
                        if (saved == null) throw new InvalidOperationException($"Target Scheduler could not save exposure template '{name}'.");
                        template = saved;
                        createdTemplates.Add(template);
                        templates.Add(template);
                    }
                    templateByExposure[exp] = template;
                }

                var project = Activator.CreateInstance(projectType, profileId)
                              ?? throw new InvalidOperationException("Could not instantiate a Target Scheduler Project.");
                PopulateProject(project, plan, activateProject);

                var target = Activator.CreateInstance(targetType)
                             ?? throw new InvalidOperationException("Could not instantiate a Target Scheduler Target.");
                ReflectionUtil.Set(target, "Name", plan.TargetName);
                ReflectionUtil.Set(target, "Enabled", true);
                ReflectionUtil.Set(target, "ra", plan.RaHours);
                ReflectionUtil.Set(target, "dec", plan.DecDeg);
                ReflectionUtil.Set(target, "rotation", plan.RotationDegrees);
                ReflectionUtil.Set(target, "roi", 100.0);

                var exposurePlans = ReflectionUtil.Get(target, "ExposurePlans") as IList
                                    ?? throw new InvalidOperationException("Target Scheduler Target.ExposurePlans is unavailable.");
                foreach (var exp in plan.Exposures) {
                    var ep = Activator.CreateInstance(exposurePlanType, profileId)
                             ?? throw new InvalidOperationException("Could not instantiate a Target Scheduler ExposurePlan.");
                    var templateId = ReflectionUtil.Get<int>(templateByExposure[exp], "Id");
                    ReflectionUtil.Set(ep, "ExposureTemplateId", templateId);
                    ReflectionUtil.Set(ep, "Exposure", exp.SubSeconds);
                    ReflectionUtil.Set(ep, "Desired", exp.DesiredCount);
                    ReflectionUtil.Set(ep, "Acquired", 0);
                    ReflectionUtil.Set(ep, "Accepted", 0);
                    ReflectionUtil.Set(ep, "IsEnabled", true);
                    exposurePlans.Add(ep);
                }

                var targets = ReflectionUtil.Get(project, "Targets") as IList
                              ?? throw new InvalidOperationException("Target Scheduler Project.Targets is unavailable.");
                targets.Add(target);

                var savedProject = ReflectionUtil.Invoke(context, "AddNewProject", project);
                if (savedProject == null) {
                    CleanupTemplates(context, createdTemplates);
                    return new TargetSchedulerCreateResult {
                        Success = false,
                        Message = "Target Scheduler rejected the new project. Its database log may contain the detailed validation error."
                    };
                }

                var projectName = ReflectionUtil.Get<string>(savedProject, "Name") ?? ReflectionUtil.Get<string>(project, "Name") ?? "";
                return new TargetSchedulerCreateResult {
                    Success = true,
                    ProjectName = projectName,
                    Message = $"Created '{projectName}' with {plan.Exposures.Count} exposure plan(s)."
                };
            } catch {
                if (context != null) CleanupTemplates(context, createdTemplates);
                throw;
            } finally {
                (context as IDisposable)?.Dispose();
            }
        } finally {
            WriteGate.Release();
        }
    }

    private static object CreateInteraction(Assembly a) {
        var type = a.GetType("NINA.Plugin.TargetScheduler.Database.SchedulerDatabaseInteraction", true)!;
        return Activator.CreateInstance(type) ?? throw new InvalidOperationException("Could not create SchedulerDatabaseInteraction.");
    }

    private static void BackupDatabase(Assembly a) {
        try {
            var type = a.GetType("NINA.Plugin.TargetScheduler.Database.SchedulerDatabaseInteraction");
            type?.GetMethod("BackupDatabase", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        } catch {
        }
    }

    private static bool IsCompatible(Version? version) => version != null && version.Major == 5 && version.Minor == 9;

    private static object? FindMatchingTemplate(IEnumerable<object> templates, ExposureRecommendation exp) {
        foreach (var t in templates) {
            if (!string.Equals(ReflectionUtil.Get<string>(t, "FilterName"), exp.Filter, StringComparison.OrdinalIgnoreCase)) continue;
            if (Math.Abs(ReflectionUtil.Get<double>(t, "DefaultExposure") - exp.SubSeconds) > 0.01) continue;
            if (ReflectionUtil.Get<int>(t, "Gain") != (exp.Gain ?? -1)) continue;
            if (ReflectionUtil.Get<int>(t, "Offset") != (exp.Offset ?? -1)) continue;
            var binObj = ReflectionUtil.Get(t, "bin");
            var bin = binObj == null ? 1 : Convert.ToInt32(binObj);
            if (bin != exp.Binning) continue;
            if (ReflectionUtil.Get<int>(t, "ReadoutMode") != (exp.ReadoutMode ?? -1)) continue;
            if (ReflectionUtil.Get<bool>(t, "MoonAvoidanceEnabled") != exp.MoonAvoidanceEnabled) continue;
            if (Math.Abs(ReflectionUtil.Get<double>(t, "MoonAvoidanceSeparation") - exp.MoonSeparationDeg) > 0.1) continue;
            if (ReflectionUtil.Get<int>(t, "MoonAvoidanceWidth") != exp.MoonWidthDays) continue;
            return t;
        }
        return null;
    }

    private static void PopulateExposureTemplate(object template, ExposureRecommendation exp) {
        ReflectionUtil.Set(template, "DefaultExposure", exp.SubSeconds);
        ReflectionUtil.Set(template, "Gain", exp.Gain ?? -1);
        ReflectionUtil.Set(template, "Offset", exp.Offset ?? -1);
        ReflectionUtil.Set(template, "bin", (int?)exp.Binning);
        ReflectionUtil.Set(template, "ReadoutMode", exp.ReadoutMode ?? -1);
        ReflectionUtil.Set(template, "twilightlevel_col", exp.Twilight switch {
            "Astronomical" => 1, "Nautical" => 2, "Civil" => 3, _ => 0
        });
        ReflectionUtil.Set(template, "MoonAvoidanceEnabled", exp.MoonAvoidanceEnabled);
        ReflectionUtil.Set(template, "MoonAvoidanceSeparation", exp.MoonSeparationDeg);
        ReflectionUtil.Set(template, "MoonAvoidanceWidth", exp.MoonWidthDays);
        ReflectionUtil.Set(template, "MoonRelaxScale", exp.MoonRelaxScale);
        ReflectionUtil.Set(template, "MoonDownEnabled", exp.MoonDownEnabled);
        ReflectionUtil.Set(template, "DitherEvery", -1);
    }

    private static void PopulateProject(object project, ImagingPlan plan, bool activate) {
        ReflectionUtil.Set(project, "Name", $"NightSage - {plan.TargetName}");
        ReflectionUtil.Set(project, "Description", $"Created by NightSage 0.1.0-alpha.1. {plan.StrategySummary}".Trim());
        ReflectionUtil.Set(project, "State", activate ? "Active" : "Draft");
        ReflectionUtil.Set(project, "Priority", plan.ProjectPriority);
        if (activate) ReflectionUtil.Set(project, "ActiveDate", (DateTime?)DateTime.Now);
        ReflectionUtil.Set(project, "MinimumTime", plan.MinimumSessionMinutes);
        ReflectionUtil.Set(project, "MinimumAltitude", plan.MinimumAltitudeDegrees);
        ReflectionUtil.Set(project, "MaximumAltitude", 0.0);
        ReflectionUtil.Set(project, "UseCustomHorizon", false);
        ReflectionUtil.Set(project, "HorizonOffset", 0.0);
        ReflectionUtil.Set(project, "MeridianWindow", 0);
        ReflectionUtil.Set(project, "FilterSwitchFrequency", plan.FilterSwitchFrequency);
        ReflectionUtil.Set(project, "DitherEvery", plan.DitherEvery);
        ReflectionUtil.Set(project, "SmartExposureOrder", plan.SmartExposureOrder);
        ReflectionUtil.Set(project, "EnableGrader", true);
        ReflectionUtil.Set(project, "IsMosaic", false);
        ReflectionUtil.Set(project, "FlatsHandling", 0);
    }

    private static string BuildTemplateName(ExposureRecommendation exp) {
        var gain = exp.Gain?.ToString() ?? "cam";
        var offset = exp.Offset?.ToString() ?? "cam";
        return $"NightSage {exp.Filter} {exp.SubSeconds:0}s G{gain} O{offset} B{exp.Binning}";
    }

    private static void CleanupTemplates(object context, IEnumerable<object> templates) {
        foreach (var template in templates.Reverse()) {
            try { ReflectionUtil.Invoke(context, "DeleteExposureTemplate", template); } catch { }
        }
    }

    private static bool EquivalentName(string a, string b) {
        static string N(string s) => new string((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        var x = N(a); var y = N(b);
        return x.Length > 0 && x == y;
    }
}
