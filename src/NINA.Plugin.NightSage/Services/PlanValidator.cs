using NINA.Plugin.NightSage.Models;
using System.Text;

namespace NINA.Plugin.NightSage.Services;

public sealed class PlanValidator {
    public ImagingPlan Validate(ImagingPlan plan, SetupContext setup) {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (setup == null) throw new ArgumentNullException(nameof(setup));
        if (plan.RaHours < 0 || plan.RaHours >= 24) throw new InvalidOperationException("Target RA is outside 0-24h.");
        if (plan.DecDeg < -90 || plan.DecDeg > 90) throw new InvalidOperationException("Target declination is outside -90..+90°.");

        plan.MinimumAltitudeDegrees = Clamp(plan.MinimumAltitudeDegrees, 15, 80);
        plan.MinimumSessionMinutes = (int)Clamp(plan.MinimumSessionMinutes, 10, 720);
        plan.RotationDegrees = NormalizeDeg(plan.RotationDegrees);
        plan.FilterSwitchFrequency = (int)Clamp(plan.FilterSwitchFrequency, 0, 20);
        plan.DitherEvery = (int)Clamp(plan.DitherEvery, 0, 50);
        plan.ProjectPriority = NormalizePriority(plan.ProjectPriority);

        var validated = new List<ExposureRecommendation>();
        foreach (var exposure in plan.Exposures ?? new()) {
            var requested = exposure.Filter?.Trim() ?? "";
            string? actual;
            if (setup.Filters.Count == 0) {
                actual = string.IsNullOrWhiteSpace(requested) ? "OSC" : requested;
                if (!plan.Warnings.Contains("No N.I.N.A. filters are configured. Preview works, but Target Scheduler creation requires a configured dummy filter for color-camera/no-wheel setups."))
                    plan.Warnings.Add("No N.I.N.A. filters are configured. Preview works, but Target Scheduler creation requires a configured dummy filter for color-camera/no-wheel setups.");
            } else {
                actual = FilterMatcher.Match(requested, setup.Filters);
                if (actual == null) {
                    plan.Warnings.Add($"Dropped requested filter '{requested}' because it is not configured in the active N.I.N.A. profile.");
                    continue;
                }
            }

            exposure.Filter = actual;
            exposure.SubSeconds = Clamp(exposure.SubSeconds, 1, 3600);
            exposure.TotalMinutes = Clamp(exposure.TotalMinutes, exposure.SubSeconds / 60.0, 12000);
            exposure.DesiredCount = Math.Max(1, (int)Math.Ceiling(exposure.TotalMinutes * 60.0 / exposure.SubSeconds));
            exposure.Binning = (int)Clamp(exposure.Binning <= 0 ? 1 : exposure.Binning, 1, 4);
            exposure.Gain = ClampNullable(exposure.Gain ?? setup.DefaultGain, setup.GainMin, setup.GainMax);
            exposure.Offset = ClampNullable(exposure.Offset ?? setup.DefaultOffset, setup.OffsetMin, setup.OffsetMax);
            exposure.ReadoutMode ??= setup.ReadoutMode;
            exposure.MoonSeparationDeg = Clamp(exposure.MoonSeparationDeg, 0, 180);
            exposure.MoonWidthDays = (int)Clamp(exposure.MoonWidthDays, 0, 14);
            exposure.MoonRelaxScale = Clamp(exposure.MoonRelaxScale, 0, 10);
            exposure.Twilight = NormalizeTwilight(exposure.Twilight);
            validated.Add(exposure);
        }

        // HDR and bright-star plans may legitimately contain several exposure lengths for the same filter.
        plan.Exposures = validated
            .GroupBy(x => $"{x.Filter.ToUpperInvariant()}|{x.SubSeconds:0.###}")
            .Select(g => g.First())
            .ToList();

        if (plan.Exposures.Count == 0) throw new InvalidOperationException(setup.Filters.Count == 0
            ? "No usable exposure was returned. Configure a dummy N.I.N.A. filter if you want Target Scheduler creation with a color camera and no filter wheel."
            : "The LLM did not return any exposure using a filter configured in the active N.I.N.A. profile.");

        if (setup.FieldWidthDeg > 0 && setup.FieldHeightDeg > 0 && plan.AngularWidthArcmin > 0 && plan.AngularHeightArcmin > 0) {
            var fit = AstronomyMath.FieldFitScore(setup, new TargetCandidate { AngularWidthArcmin = plan.AngularWidthArcmin, AngularHeightArcmin = plan.AngularHeightArcmin });
            if (fit < 0.35) plan.Warnings.Add("The target is a poor fit for the current sensor/focal length; consider a mosaic or a different setup.");
        }
        plan.IsValidated = true;
        return plan;
    }

    private static int? ClampNullable(int? value, int? min, int? max) {
        if (!value.HasValue) return null;
        var v = value.Value;
        if (min.HasValue) v = Math.Max(v, min.Value);
        if (max.HasValue) v = Math.Min(v, max.Value);
        return v;
    }
    private static string NormalizePriority(string? value) => value?.Trim().ToLowerInvariant() switch { "low" => "Low", "high" => "High", _ => "Normal" };
    private static string NormalizeTwilight(string? value) => value?.Trim().ToLowerInvariant() switch { "civil" => "Civil", "nautical" => "Nautical", "astronomical" => "Astronomical", _ => "Nighttime" };
    private static double NormalizeDeg(double value) { value %= 360; return value < 0 ? value + 360 : value; }
    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
}

public static class FilterMatcher {
    public static string? Match(string requested, IReadOnlyCollection<string> available) {
        if (available.Count == 0) return null;
        if (string.IsNullOrWhiteSpace(requested)) return available.Count == 1 ? available.First() : null;
        var exact = available.FirstOrDefault(x => string.Equals(x.Trim(), requested.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;
        var req = Canonical(requested);
        var normalized = available.Select(x => (Original: x, Canonical: Canonical(x))).ToList();
        var canonical = normalized.FirstOrDefault(x => x.Canonical == req);
        if (!string.IsNullOrEmpty(canonical.Original)) return canonical.Original;
        if (available.Count == 1 && (req is "osc" or "color" or "broadband" or "clear")) return available.First();
        return null;
    }

    private static string Canonical(string value) {
        var sb = new StringBuilder();
        foreach (var ch in value.ToLowerInvariant().Normalize(NormalizationForm.FormD)) {
            if (char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        }
        return sb.ToString().Replace("hydrogenalpha","ha").Replace("halpha","ha").Replace("hydrogen","h")
            .Replace("oxygeniii","oiii").Replace("oxygen3","oiii").Replace("sulfurii","sii").Replace("sulphurii","sii")
            .Replace("sulfur2","sii").Replace("sulphur2","sii").Replace("luminance","l").Replace("red","r").Replace("green","g").Replace("blue","b");
    }
}
