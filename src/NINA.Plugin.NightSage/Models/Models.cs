using System.Collections.ObjectModel;

namespace NINA.Plugin.NightSage.Models;

public enum AutonomyMode {
    Preview,
    Create,
    Autopilot
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

    public string Summary => $"{Filter}: {DesiredCount} × {SubSeconds:0}s ({TotalMinutes / 60.0:0.0}h)";
}

public sealed class ImagingPlan {
    public string TargetName { get; set; } = "";
    public string TargetType { get; set; } = "";
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
    public string Reason { get; set; } = "";
    public double ModelScore { get; set; }
    public double DeterministicScore { get; set; }
    public double TotalScore { get; set; }
    public bool AlreadyInTargetScheduler { get; set; }
    public VisibilitySummary Visibility { get; set; } = new();

    public string DisplayLine => $"{Category}: {Name} — score {TotalScore:0} · max alt {Visibility.MaxAltitudeDeg:0}° · {Visibility.DarkHoursAboveMinimum:0.0}h dark/usable";
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
