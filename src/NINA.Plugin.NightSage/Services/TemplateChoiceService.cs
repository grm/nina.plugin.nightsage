using NINA.Plugin.NightSage.Models;

namespace NINA.Plugin.NightSage.Services;

public static class TemplateChoiceService {
    public static List<ExposureTemplateChoice> Build(ImagingPlan plan, IReadOnlyList<TargetSchedulerTemplateInfo> templates) {
        var result = new List<ExposureTemplateChoice>();
        foreach (var exposure in plan.Exposures) {
            var sameFilter = templates
                .Where(t => string.Equals(t.FilterName, exposure.Filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => Score(t, exposure))
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var options = sameFilter.Select(t => new ExposureTemplateOption {
                Template = t,
                DisplayLabel = BuildLabel(t, exposure)
            }).ToList();

            options.Add(new ExposureTemplateOption {
                Template = null,
                DisplayLabel = $"Create new {exposure.Filter} {exposure.SubSeconds:0.#}s from plan settings"
            });

            ExposureTemplateOption? selected = null;
            if (!string.IsNullOrWhiteSpace(exposure.PreferredTemplateName)) {
                selected = options.FirstOrDefault(o => o.Template != null &&
                    string.Equals(o.Template.Name, exposure.PreferredTemplateName, StringComparison.OrdinalIgnoreCase));
            }
            selected ??= options.FirstOrDefault();

            result.Add(new ExposureTemplateChoice {
                Exposure = exposure,
                Options = options,
                SelectedOption = selected
            });
        }
        return result;
    }

    private static string BuildLabel(TargetSchedulerTemplateInfo t, ExposureRecommendation e) {
        var technical = $"{t.Name} · {t.DefaultExposure:0.#}s · G{t.Gain} O{t.Offset} · B{t.Binning}";
        return Math.Abs(t.DefaultExposure - e.SubSeconds) < 0.01
            ? technical + " · existing"
            : technical + $" · base → create {e.SubSeconds:0.#}s";
    }

    private static double Score(TargetSchedulerTemplateInfo t, ExposureRecommendation e) {
        if (Math.Abs(t.DefaultExposure - e.SubSeconds) < 0.01) return -10000;
        var duration = Math.Abs(Math.Log(Math.Max(1, t.DefaultExposure) / Math.Max(1, e.SubSeconds))) * 100;
        if (e.Gain.HasValue && t.Gain != e.Gain.Value) duration += 8;
        if (e.Offset.HasValue && t.Offset != e.Offset.Value) duration += 5;
        if (t.Binning != e.Binning) duration += 15;
        if (e.ReadoutMode.HasValue && t.ReadoutMode != e.ReadoutMode.Value) duration += 8;
        return duration;
    }
}
