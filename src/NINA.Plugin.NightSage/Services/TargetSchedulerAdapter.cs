using NINA.Plugin.NightSage.Infrastructure;
using NINA.Plugin.NightSage.Models;
using System.Collections;
using System.Reflection;

namespace NINA.Plugin.NightSage.Services;

public interface ITargetSchedulerAdapter {
    string Status { get; }
    bool CanWrite { get; }
    IReadOnlyList<ExistingTargetInfo> GetExistingTargets(string profileId);
    IReadOnlyList<TargetSchedulerTemplateInfo> GetExposureTemplates(string profileId);
    Task<TargetSchedulerCreateResult> CreateAsync(string profileId, ImagingPlan plan, IReadOnlyList<ExposureTemplateChoice> choices, bool activateProject, CancellationToken cancellationToken);
}

public sealed class TargetSchedulerReflectionAdapter : ITargetSchedulerAdapter {
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    private Assembly? Assembly => AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => string.Equals(a.GetName().Name, "NINA.Plugin.TargetScheduler", StringComparison.OrdinalIgnoreCase));

    public bool CanWrite => Assembly is { } a && IsCompatible(a.GetName().Version);
    public string Status {
        get {
            var a = Assembly;
            if (a == null) return "Target Scheduler waiting to load…";
            var version = a.GetName().Version;
            if (!IsCompatible(version)) return $"Target Scheduler {version} detected — NightSage supports 5.9.x";
            return $"Target Scheduler {version} ready";
        }
    }

    public IReadOnlyList<ExistingTargetInfo> GetExistingTargets(string profileId) {
        var result = new List<ExistingTargetInfo>();
        var a = Assembly;
        if (a == null || !IsCompatible(a.GetName().Version)) return result;
        object? context = null;
        try {
            context = ReflectionUtil.Invoke(CreateInteraction(a), "GetContext");
            foreach (var project in ReflectionUtil.AsObjects(ReflectionUtil.Invoke(context!, "GetAllProjects", profileId))) {
                var projectName = ReflectionUtil.Get<string>(project, "Name") ?? "";
                var state = ReflectionUtil.Get(project, "State")?.ToString() ?? "";
                foreach (var target in ReflectionUtil.AsObjects(ReflectionUtil.Get(project, "Targets"))) result.Add(new ExistingTargetInfo {
                    ProjectName = projectName,
                    TargetName = ReflectionUtil.Get<string>(target, "Name") ?? "",
                    PercentComplete = ReflectionUtil.Get<double>(target, "PercentComplete"),
                    ProjectActive = string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase)
                });
            }
        } catch { return Array.Empty<ExistingTargetInfo>(); }
        finally { (context as IDisposable)?.Dispose(); }
        return result;
    }

    public IReadOnlyList<TargetSchedulerTemplateInfo> GetExposureTemplates(string profileId) {
        var a = Assembly;
        if (a == null || !IsCompatible(a.GetName().Version)) return Array.Empty<TargetSchedulerTemplateInfo>();
        object? context = null;
        try {
            context = ReflectionUtil.Invoke(CreateInteraction(a), "GetContext");
            return ReflectionUtil.AsObjects(ReflectionUtil.Invoke(context!, "GetExposureTemplates", profileId))
                .Select(ToTemplateInfo).OrderBy(x => x.FilterName).ThenBy(x => x.DefaultExposure).ThenBy(x => x.Name).ToList();
        } catch { return Array.Empty<TargetSchedulerTemplateInfo>(); }
        finally { (context as IDisposable)?.Dispose(); }
    }

    public async Task<TargetSchedulerCreateResult> CreateAsync(
        string profileId,
        ImagingPlan plan,
        IReadOnlyList<ExposureTemplateChoice> choices,
        bool activateProject,
        CancellationToken cancellationToken) {

        if (!plan.IsValidated) throw new InvalidOperationException("NightSage will not write an unvalidated plan.");
        if (plan.Exposures.Count == 0) throw new InvalidOperationException("The plan has no exposures.");
        var a = Assembly ?? throw new InvalidOperationException("Target Scheduler is not loaded yet.");
        if (!IsCompatible(a.GetName().Version)) throw new InvalidOperationException(Status);

        var plannedPanels = GetPlannedPanels(plan);
        var existing = GetExistingTargets(profileId);
        var duplicate = plannedPanels.FirstOrDefault(panel => existing.Any(x => EquivalentName(x.TargetName, panel.Name)));
        if (duplicate != null)
            return new TargetSchedulerCreateResult {
                Success = false,
                Message = $"Target '{duplicate.Name}' already exists in Target Scheduler for the active profile. NightSage will not create a duplicate."
            };

        await WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            BackupDatabase(a);

            object? context = null;
            var createdTemplates = new List<object>();
            try {
                context = ReflectionUtil.Invoke(CreateInteraction(a), "GetContext") ?? throw new InvalidOperationException("Target Scheduler returned no database context.");
                var ns = "NINA.Plugin.TargetScheduler.Database.Schema.";
                var projectType = a.GetType(ns + "Project", true)!;
                var targetType = a.GetType(ns + "Target", true)!;
                var exposurePlanType = a.GetType(ns + "ExposurePlan", true)!;
                var exposureTemplateType = a.GetType(ns + "ExposureTemplate", true)!;
                var templates = ReflectionUtil.AsObjects(ReflectionUtil.Invoke(context, "GetExposureTemplates", profileId)).ToList();
                var templateByExposure = new Dictionary<ExposureRecommendation, object>();

                foreach (var exp in plan.Exposures) {
                    cancellationToken.ThrowIfCancellationRequested();
                    var choice = choices.FirstOrDefault(c => ReferenceEquals(c.Exposure, exp))
                                 ?? TemplateChoiceService.Build(new ImagingPlan { Exposures = new List<ExposureRecommendation> { exp } }, templates.Select(ToTemplateInfo).ToList())[0];
                    object? template = null;
                    if (choice.SelectedTemplate != null)
                        template = templates.FirstOrDefault(t => ReflectionUtil.Get<int>(t, "Id") == choice.SelectedTemplate.Id);

                    if (template == null) {
                        template = Activator.CreateInstance(exposureTemplateType, profileId, BuildNewTemplateName(exp), exp.Filter)
                                   ?? throw new InvalidOperationException("Could not instantiate a Target Scheduler ExposureTemplate.");
                        PopulateFromPlan(template, exp);
                        template = SaveTemplate(context, template, createdTemplates);
                        templates.Add(template);
                    } else if (Math.Abs(ReflectionUtil.Get<double>(template, "DefaultExposure") - exp.SubSeconds) >= 0.01) {
                        var baseName = ReflectionUtil.Get<string>(template, "Name") ?? exp.Filter;
                        var derived = Activator.CreateInstance(exposureTemplateType, profileId, BuildDerivedTemplateName(exp, baseName), exp.Filter)
                                      ?? throw new InvalidOperationException("Could not instantiate a derived Target Scheduler ExposureTemplate.");
                        CopyTechnicalSettings(template, derived);
                        ReflectionUtil.Set(derived, "DefaultExposure", exp.SubSeconds);
                        template = SaveTemplate(context, derived, createdTemplates);
                        templates.Add(template);
                    }
                    templateByExposure[exp] = template;
                }

                var project = Activator.CreateInstance(projectType, profileId) ?? throw new InvalidOperationException("Could not instantiate a Target Scheduler Project.");
                PopulateProject(project, plan, activateProject, plannedPanels.Count > 1);

                var targets = ReflectionUtil.Get(project, "Targets") as IList ?? throw new InvalidOperationException("Target Scheduler Project.Targets is unavailable.");
                foreach (var panel in plannedPanels) {
                    cancellationToken.ThrowIfCancellationRequested();
                    var target = Activator.CreateInstance(targetType) ?? throw new InvalidOperationException("Could not instantiate a Target Scheduler Target.");
                    ReflectionUtil.Set(target, "Name", panel.Name);
                    ReflectionUtil.Set(target, "Enabled", true);
                    ReflectionUtil.Set(target, "ra", panel.RaHours);
                    ReflectionUtil.Set(target, "dec", panel.DecDeg);
                    ReflectionUtil.Set(target, "rotation", panel.RotationDegrees);
                    ReflectionUtil.Set(target, "roi", 100.0);

                    var exposurePlans = ReflectionUtil.Get(target, "ExposurePlans") as IList
                                        ?? throw new InvalidOperationException("Target Scheduler Target.ExposurePlans is unavailable.");
                    foreach (var exp in plan.Exposures) {
                        var ep = Activator.CreateInstance(exposurePlanType, profileId)
                                 ?? throw new InvalidOperationException("Could not instantiate a Target Scheduler ExposurePlan.");
                        ReflectionUtil.Set(ep, "ExposureTemplateId", ReflectionUtil.Get<int>(templateByExposure[exp], "Id"));
                        ReflectionUtil.Set(ep, "Exposure", -1.0);
                        ReflectionUtil.Set(ep, "Desired", exp.DesiredCount);
                        ReflectionUtil.Set(ep, "Acquired", 0);
                        ReflectionUtil.Set(ep, "Accepted", 0);
                        ReflectionUtil.Set(ep, "IsEnabled", true);
                        exposurePlans.Add(ep);
                    }
                    targets.Add(target);
                }

                var savedProject = ReflectionUtil.Invoke(context, "AddNewProject", project);
                if (savedProject == null) {
                    CleanupTemplates(context, createdTemplates);
                    return new TargetSchedulerCreateResult { Success = false, Message = "Target Scheduler rejected the new project." };
                }

                var projectName = ReflectionUtil.Get<string>(savedProject, "Name") ?? ReflectionUtil.Get<string>(project, "Name") ?? "";
                var detail = plannedPanels.Count > 1
                    ? $"{plannedPanels.Count} mosaic panels, {plan.Exposures.Count} exposure plan(s) per panel"
                    : $"{plan.Exposures.Count} exposure plan(s)";

                return new TargetSchedulerCreateResult {
                    Success = true,
                    ProjectName = projectName,
                    Message = $"Created '{projectName}' with {detail}."
                };
            } catch {
                if (context != null) CleanupTemplates(context, createdTemplates);
                throw;
            } finally { (context as IDisposable)?.Dispose(); }
        } finally { WriteGate.Release(); }
    }

    private static List<FramingPanel> GetPlannedPanels(ImagingPlan plan) {
        if (plan.Framing?.Panels.Count > 0) {
            return plan.Framing.Panels.Select((p, index) => new FramingPanel {
                Index = p.Index > 0 ? p.Index : index + 1,
                Name = plan.Framing.PanelCount > 1
                    ? (string.IsNullOrWhiteSpace(p.Name) ? $"{plan.TargetName} Panel {index + 1}" : p.Name)
                    : plan.TargetName,
                RaHours = p.RaHours,
                DecDeg = p.DecDeg,
                RotationDegrees = p.RotationDegrees
            }).ToList();
        }

        return new List<FramingPanel> {
            new() {
                Index = 1,
                Name = plan.TargetName,
                RaHours = plan.RaHours,
                DecDeg = plan.DecDeg,
                RotationDegrees = plan.RotationDegrees
            }
        };
    }

    private static TargetSchedulerTemplateInfo ToTemplateInfo(object t) {
        var binObj = ReflectionUtil.Get(t, "bin");
        return new TargetSchedulerTemplateInfo {
            Id = ReflectionUtil.Get<int>(t, "Id"), Name = ReflectionUtil.Get<string>(t, "Name") ?? "",
            FilterName = ReflectionUtil.Get<string>(t, "FilterName") ?? "", DefaultExposure = ReflectionUtil.Get<double>(t, "DefaultExposure"),
            Gain = ReflectionUtil.Get<int>(t, "Gain"), Offset = ReflectionUtil.Get<int>(t, "Offset"),
            Binning = binObj == null ? 1 : Convert.ToInt32(binObj), ReadoutMode = ReflectionUtil.Get<int>(t, "ReadoutMode"),
            TwilightLevel = Convert.ToInt32(ReflectionUtil.Get(t, "twilightlevel_col") ?? 0), MinutesOffset = ReflectionUtil.Get<int>(t, "MinutesOffset"),
            MoonAvoidanceEnabled = ReflectionUtil.Get<bool>(t, "MoonAvoidanceEnabled"), MoonAvoidanceSeparation = ReflectionUtil.Get<double>(t, "MoonAvoidanceSeparation"),
            MoonAvoidanceWidth = ReflectionUtil.Get<int>(t, "MoonAvoidanceWidth"), MoonRelaxScale = ReflectionUtil.Get<double>(t, "MoonRelaxScale"),
            MoonRelaxMaxAltitude = ReflectionUtil.Get<double>(t, "MoonRelaxMaxAltitude"), MoonRelaxMinAltitude = ReflectionUtil.Get<double>(t, "MoonRelaxMinAltitude"),
            MoonDownEnabled = ReflectionUtil.Get<bool>(t, "MoonDownEnabled"), DitherEvery = ReflectionUtil.Get<int>(t, "DitherEvery"),
            MaximumHumidity = ReflectionUtil.Get<double>(t, "MaximumHumidity")
        };
    }

    private static object SaveTemplate(object context, object template, List<object> created) {
        var saved = ReflectionUtil.Invoke(context, "SaveExposureTemplate", template)
                    ?? throw new InvalidOperationException("Target Scheduler could not save an exposure template.");
        created.Add(saved);
        return saved;
    }

    private static void CopyTechnicalSettings(object source, object dest) {
        foreach (var name in new[] {
                     "Gain","Offset","bin","ReadoutMode","twilightlevel_col","MinutesOffset",
                     "MoonAvoidanceEnabled","MoonAvoidanceSeparation","MoonAvoidanceWidth","MoonRelaxScale",
                     "MoonRelaxMaxAltitude","MoonRelaxMinAltitude","MoonDownEnabled","DitherEvery","MaximumHumidity"
                 })
            ReflectionUtil.Set(dest, name, ReflectionUtil.Get(source, name));
    }

    private static void PopulateFromPlan(object template, ExposureRecommendation exp) {
        ReflectionUtil.Set(template, "DefaultExposure", exp.SubSeconds);
        ReflectionUtil.Set(template, "Gain", exp.Gain ?? -1);
        ReflectionUtil.Set(template, "Offset", exp.Offset ?? -1);
        ReflectionUtil.Set(template, "bin", (int?)exp.Binning);
        ReflectionUtil.Set(template, "ReadoutMode", exp.ReadoutMode ?? -1);
        ReflectionUtil.Set(template, "twilightlevel_col", exp.Twilight switch { "Astronomical" => 1, "Nautical" => 2, "Civil" => 3, _ => 0 });
        ReflectionUtil.Set(template, "MoonAvoidanceEnabled", exp.MoonAvoidanceEnabled);
        ReflectionUtil.Set(template, "MoonAvoidanceSeparation", exp.MoonSeparationDeg);
        ReflectionUtil.Set(template, "MoonAvoidanceWidth", exp.MoonWidthDays);
        ReflectionUtil.Set(template, "MoonRelaxScale", exp.MoonRelaxScale);
        ReflectionUtil.Set(template, "MoonDownEnabled", exp.MoonDownEnabled);
    }

    private static object CreateInteraction(Assembly a) =>
        Activator.CreateInstance(a.GetType("NINA.Plugin.TargetScheduler.Database.SchedulerDatabaseInteraction", true)!)!;

    private static void BackupDatabase(Assembly a) {
        try {
            a.GetType("NINA.Plugin.TargetScheduler.Database.SchedulerDatabaseInteraction")
                ?.GetMethod("BackupDatabase", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, null);
        } catch { }
    }

    private static bool IsCompatible(Version? version) => version != null && version.Major == 5 && version.Minor == 9;
    private static string BuildNewTemplateName(ExposureRecommendation e) => $"NightSage {e.Filter} {e.SubSeconds:0.#}s";
    private static string BuildDerivedTemplateName(ExposureRecommendation e, string baseName) => $"NightSage {e.Filter} {e.SubSeconds:0.#}s ← {baseName}";

    private static void CleanupTemplates(object context, IEnumerable<object> templates) {
        foreach (var t in templates.Reverse())
            try { ReflectionUtil.Invoke(context, "DeleteExposureTemplate", t); } catch { }
    }

    private static bool EquivalentName(string a, string b) {
        static string N(string s) => new string((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        var x = N(a);
        var y = N(b);
        return x.Length > 0 && x == y;
    }

    private static void PopulateProject(object project, ImagingPlan plan, bool activate, bool isMosaic) {
        ReflectionUtil.Set(project, "Name", $"NightSage - {plan.TargetName}");
        ReflectionUtil.Set(project, "Description", $"Created by NightSage 0.1.2-alpha. {plan.StrategySummary}".Trim());
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
        ReflectionUtil.Set(project, "IsMosaic", isMosaic);
        ReflectionUtil.Set(project, "FlatsHandling", 0);
    }
}
