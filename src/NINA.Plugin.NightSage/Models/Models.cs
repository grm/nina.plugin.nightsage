using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NINA.Plugin.NightSage.Models;

public enum AutonomyMode { Preview, Create, Autopilot }
public enum IntegrationAmbition { Quick, Balanced, Deep }

public static class IntegrationAmbitionPolicy {
    public static string PlanningGuidance(IntegrationAmbition ambition) => ambition switch {
        IntegrationAmbition.Quick =>
            "Optimize for a compact project. Prefer a compelling result in about 10 hours or less. Shorter plans are welcome. " +
            "Do not sacrifice the core quality of the target merely to hit 10h; a modest overrun is allowed when clearly justified.",
        IntegrationAmbition.Balanced =>
            "There is no minimum integration time. Prefer quality-efficient projects that generally fit within about 20 hours. " +
            "A 4h, 8h or 10h plan is fully valid when that is enough; never inflate integration just to fill a 10-20h range. " +
            "Exceed about 20h only when the target genuinely benefits enough to justify it.",
        _ =>
            "Integration time is not a limiting factor. Choose the amount of data genuinely justified by this target and setup. " +
            "Short projects remain fully valid: do not add hours merely because Deep was selected. Long 20h, 30h or 50h projects are allowed when faint structures or the intended result truly benefit."
    };

    public static string DiscoveryGuidance(IntegrationAmbition ambition) => ambition switch {
        IntegrationAmbition.Quick =>
            "Favor targets that can produce a compelling result with this exact setup in roughly 10 hours or less. " +
            "Do not reject a slightly longer exceptional target, but prefer shorter high-return projects.",
        IntegrationAmbition.Balanced =>
            "No minimum duration. Targets that are excellent in 5-10 hours remain first-class choices. " +
            "Also allow targets that benefit from moderate integrations up to roughly 20 hours. Do not prefer a target merely because it needs longer.",
        _ =>
            "Do not penalize a target because it could justify a long integration. 20h+ projects are allowed, but short 5-10h targets remain equally eligible and may rank first. " +
            "Never reward duration itself; rank photographic suitability and expected result."
    };

    public static double DiscoveryScoreAdjustment(IntegrationAmbition ambition, double estimatedHours) {
        if (estimatedHours <= 0) return 0;
        return ambition switch {
            IntegrationAmbition.Quick when estimatedHours > 10 => -Math.Min(30, (estimatedHours - 10) * 2.5),
            IntegrationAmbition.Balanced when estimatedHours > 20 => -Math.Min(20, (estimatedHours - 20) * 1.5),
            _ => 0
        };
    }

    public static string? OverrunWarning(IntegrationAmbition ambition, double actualHours) => ambition switch {
        IntegrationAmbition.Quick when actualHours > 11 =>
            $"Planned integration is {actualHours:0.0}h, above the usual Quick target of about 10h; NightSage kept it because the proposed result may justify the overrun.",
        IntegrationAmbition.Balanced when actualHours > 22 =>
            $"Planned integration is {actualHours:0.0}h, above the usual Balanced range of about 20h; NightSage kept it because the proposed result may justify the overrun.",
        _ => null
    };
}

public static class IntegrationTimeFormatter {
    public static string FormatMinutes(double minutes) {
        var totalSeconds = Math.Max(0, (int)Math.Round(minutes * 60.0));
        var hours = totalSeconds / 3600;
        var mins = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        if (hours > 0) return seconds > 0 ? $"{hours}h {mins:00}m {seconds:00}s" : $"{hours}h {mins:00}m";
        if (mins > 0) return seconds > 0 ? $"{mins}m {seconds:00}s" : $"{mins}m";
        return $"{seconds}s";
    }
}

public sealed class SetupContext {
    public string ProfileId { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public string TelescopeName { get; set; } = "";
    public string CameraName { get; set; } = "";
    public double FocalLengthMm { get; set; }
    public double FocalRatio { get; set; }
    public double PixelSizeMicrons { get; set; }
    public int SensorWidthPixels { get; set; }
    public int SensorHeightPixels { get; set; }
    public double FieldWidthDeg { get; set; }
    public double FieldHeightDeg { get; set; }
    public double ImageScaleArcsecPerPixel { get; set; }
    public bool CameraConnected { get; set; }
    public int? DefaultGain { get; set; }
    public int? DefaultOffset { get; set; }
    public int? GainMin { get; set; }
    public int? GainMax { get; set; }
    public int? OffsetMin { get; set; }
    public int? OffsetMax { get; set; }
    public int? ReadoutMode { get; set; }
    public double LatitudeDeg { get; set; }
    public double LongitudeDeg { get; set; }
    public double ElevationM { get; set; }
    public string BayerPattern { get; set; } = "";
    public List<string> Filters { get; set; } = new();
    public string Summary => $"{ProfileName} · {TelescopeName} {FocalLengthMm:0}mm f/{FocalRatio:0.0} · {CameraName} · " +
                             (FieldWidthDeg > 0 ? $"{FieldWidthDeg:0.00}° × {FieldHeightDeg:0.00}° · " : "") +
                             $"{(Filters.Count == 0 ? "no configured filters" : string.Join(", ", Filters))}";
}

public sealed class ResolvedTarget {
    public string Query { get; set; } = "";
    public string CanonicalName { get; set; } = "";
    public double RaHours { get; set; }
    public double DecDeg { get; set; }
    public string Resolver { get; set; } = "";
}

public sealed class ExposureRecommendation {
    public string Filter { get; set; } = "";
    public double SubSeconds { get; set; }
    public double TotalMinutes { get; set; }
    public int DesiredCount { get; set; }
    public int Binning { get; set; } = 1;
    public int? Gain { get; set; }
    public int? Offset { get; set; }
    public int? ReadoutMode { get; set; }
    public bool MoonAvoidanceEnabled { get; set; }
    public double MoonSeparationDeg { get; set; } = 60;
    public int MoonWidthDays { get; set; } = 7;
    public double MoonRelaxScale { get; set; }
    public bool MoonDownEnabled { get; set; }
    public string Twilight { get; set; } = "Nighttime";
    public string PreferredTemplateName { get; set; } = "";
    public double PlannedIntegrationMinutes => DesiredCount * SubSeconds / 60.0;
    public string IntegrationDisplay => IntegrationTimeFormatter.FormatMinutes(PlannedIntegrationMinutes);
    public string Summary => $"{Filter}: {DesiredCount} × {SubSeconds:0}s ({IntegrationDisplay})";
}

public sealed class ImagingPlan {
    public string TargetName { get; set; } = "";
    public string TargetType { get; set; } = "";
    public IntegrationAmbition Ambition { get; set; } = IntegrationAmbition.Balanced;
    public double RaHours { get; set; }
    public double DecDeg { get; set; }
    public double AngularWidthArcmin { get; set; }
    public double AngularHeightArcmin { get; set; }
    public double RotationDegrees { get; set; }
    public double MinimumAltitudeDegrees { get; set; } = 30;
    public int MinimumSessionMinutes { get; set; } = 60;
    public string ProjectPriority { get; set; } = "Normal";
    public int FilterSwitchFrequency { get; set; }
    public int DitherEvery { get; set; } = 1;
    public bool SmartExposureOrder { get; set; } = true;
    public string StrategySummary { get; set; } = "";
    public List<ExposureRecommendation> Exposures { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool IsValidated { get; set; }
    public string ExposureSummary => string.Join(Environment.NewLine, Exposures.Select(x => x.Summary));
    public double TotalIntegrationMinutes => Exposures.Sum(x => x.PlannedIntegrationMinutes);
    public string TotalIntegrationDisplay => IntegrationTimeFormatter.FormatMinutes(TotalIntegrationMinutes);
    public string FilterIntegrationSummary => string.Join("  ·  ",
        Exposures
            .GroupBy(x => x.Filter, StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{g.Key}: {IntegrationTimeFormatter.FormatMinutes(g.Sum(x => x.PlannedIntegrationMinutes))}"));
}

public sealed class TargetSchedulerTemplateInfo {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string FilterName { get; set; } = "";
    public double DefaultExposure { get; set; }
    public int Gain { get; set; }
    public int Offset { get; set; }
    public int Binning { get; set; } = 1;
    public int ReadoutMode { get; set; }
    public int TwilightLevel { get; set; }
    public int MinutesOffset { get; set; }
    public bool MoonAvoidanceEnabled { get; set; }
    public double MoonAvoidanceSeparation { get; set; }
    public int MoonAvoidanceWidth { get; set; }
    public double MoonRelaxScale { get; set; }
    public double MoonRelaxMaxAltitude { get; set; }
    public double MoonRelaxMinAltitude { get; set; }
    public bool MoonDownEnabled { get; set; }
    public int DitherEvery { get; set; }
    public double MaximumHumidity { get; set; }
}

public sealed class ExposureTemplateOption {
    public TargetSchedulerTemplateInfo? Template { get; set; }
    public string DisplayLabel { get; set; } = "";
}

public sealed class ExposureTemplateChoice : INotifyPropertyChanged {
    private ExposureTemplateOption? selectedOption;
    public ExposureRecommendation Exposure { get; set; } = new();
    public IReadOnlyList<ExposureTemplateOption> Options { get; set; } = Array.Empty<ExposureTemplateOption>();
    public ExposureTemplateOption? SelectedOption {
        get => selectedOption;
        set {
            if (ReferenceEquals(selectedOption, value)) return;
            selectedOption = value;
            Raise();
            Raise(nameof(SelectedTemplate));
            Raise(nameof(UsesExistingUnchanged));
            Raise(nameof(RequiresCreation));
            Raise(nameof(IsDerived));
            Raise(nameof(ActionSummary));
        }
    }
    public TargetSchedulerTemplateInfo? SelectedTemplate => SelectedOption?.Template;
    public bool UsesExistingUnchanged => SelectedTemplate != null && Math.Abs(SelectedTemplate.DefaultExposure - Exposure.SubSeconds) < 0.01;
    public bool RequiresCreation => !UsesExistingUnchanged;
    public bool IsDerived => SelectedTemplate != null && RequiresCreation;
    public string ActionSummary => UsesExistingUnchanged
        ? $"Use existing template '{SelectedTemplate!.Name}'"
        : IsDerived
            ? $"Create {Exposure.Filter} {Exposure.SubSeconds:0.#}s from '{SelectedTemplate!.Name}' and preserve its technical settings"
            : $"Create a new {Exposure.Filter} {Exposure.SubSeconds:0.#}s template from the plan settings";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class VisibilitySummary {
    public double MaxAltitudeDeg { get; set; }
    public double DarkHoursAboveMinimum { get; set; }
    public DateTime BestUtc { get; set; }
    public double MoonSeparationAtBestDeg { get; set; }
}

public sealed class TargetCandidate {
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string TargetType { get; set; } = "";
    public double RaHours { get; set; }
    public double DecDeg { get; set; }
    public double AngularWidthArcmin { get; set; }
    public double AngularHeightArcmin { get; set; }
    public double EstimatedIntegrationHours { get; set; }
    public string Reason { get; set; } = "";
    public double ModelScore { get; set; }
    public double DeterministicScore { get; set; }
    public double TotalScore { get; set; }
    public bool AlreadyInTargetScheduler { get; set; }
    public VisibilitySummary Visibility { get; set; } = new();
    public string DisplayLine => $"{Category}: {Name} — score {TotalScore:0} · ~{EstimatedIntegrationHours:0.#}h · max alt {Visibility.MaxAltitudeDeg:0}° · {Visibility.DarkHoursAboveMinimum:0.0}h dark/usable";
}

public sealed class DiscoveryResult {
    public ObservableCollection<TargetCandidate> Candidates { get; } = new();
    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ExistingTargetInfo {
    public string ProjectName { get; set; } = "";
    public string TargetName { get; set; } = "";
    public double PercentComplete { get; set; }
    public bool ProjectActive { get; set; }
}

public sealed class TargetSchedulerCreateResult {
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string ProjectName { get; set; } = "";
}
