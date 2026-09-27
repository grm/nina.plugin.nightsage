using NINA.Plugin.NightSage.Models;

namespace NINA.Plugin.NightSage.Services;

public static class TemplateChoiceService {
    public static List<ExposureTemplateChoice> Build(ImagingPlan plan, IReadOnlyList<TargetSchedulerTemplateInfo> templates) {
        var result = new List<ExposureTemplateChoice>();
        foreach (var exposure in plan.Exposures) {
            var compatible = templates
                .Where(t => string.Equals(t.FilterName, exposure.Filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => Score(t, exposure))
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            TargetSchedulerTemplateInfo? selected = null;
            if (!string.IsNullOrWhiteSpace(exposure.PreferredTemplateName)) {
                selected = compatible.FirstOrDefault(t =>
                    string.Equals(t.Name, exposure.PreferredTemplateName, StringComparison.OrdinalIgnoreCase));
            }
            selected ??= compatible.FirstOrDefault();

            result.Add(new ExposureTemplateChoice {
                Exposure = exposure,
                CompatibleTemplates = compatible,
                SelectedTemplate = selected
            });
        }
        return result;
    }

    private static double Score(TargetSchedulerTemplateInfo t, ExposureRecommendation e) {
        if (Math.Abs(t.DefaultExposure - e.SubSeconds) < 0.01) return -10000;
        var duration = Math.Abs(Math.Log((Math.Max(1, t.DefaultExposure)) / Math.Max(1, e.SubSeconds))) * 100;
        if (e.Gain.HasValue && t.Gain != e.Gain.Value) duration += 8;
        if (e.Offset.HasValue && t.Offset != e.Offset.Value) duration += 5;
        if (t.Binning != e.Binning) duration += 15;
        if (e.ReadoutMode.HasValue && t.ReadoutMode != e.ReadoutMode.Value) duration += 8;
        return duration;
    }
}
