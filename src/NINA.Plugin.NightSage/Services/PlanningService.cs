using NINA.Plugin.NightSage.Infrastructure;
using NINA.Plugin.NightSage.Models;
using NINA.Plugin.NightSage.Providers;
using System.Globalization;
using System.Text.Json;

namespace NINA.Plugin.NightSage.Services;

public sealed class PlanningService {
    private readonly ITargetResolver resolver;
    private readonly PlanValidator validator;

    public PlanningService(ITargetResolver? resolver = null, PlanValidator? validator = null) {
        this.resolver = resolver ?? new SesameTargetResolver();
        this.validator = validator ?? new PlanValidator();
    }

    public Task<ImagingPlan> BuildPlanAsync(string targetQuery, string userPreferences, SetupContext setup, ILLMProvider provider, CancellationToken cancellationToken) =>
        BuildPlanAsync(targetQuery, userPreferences, setup, Array.Empty<TargetSchedulerTemplateInfo>(), IntegrationAmbition.Balanced, null, provider, cancellationToken);

    public Task<ImagingPlan> BuildPlanAsync(
        string targetQuery,
        string userPreferences,
        SetupContext setup,
        IReadOnlyList<TargetSchedulerTemplateInfo> templates,
        ILLMProvider provider,
        CancellationToken cancellationToken) =>
        BuildPlanAsync(targetQuery, userPreferences, setup, templates, IntegrationAmbition.Balanced, null, provider, cancellationToken);

    public Task<ImagingPlan> BuildPlanAsync(
        string targetQuery,
        string userPreferences,
        SetupContext setup,
        IReadOnlyList<TargetSchedulerTemplateInfo> templates,
        IntegrationAmbition ambition,
        ILLMProvider provider,
        CancellationToken cancellationToken) =>
        BuildPlanAsync(targetQuery, userPreferences, setup, templates, ambition, null, provider, cancellationToken);

    public async Task<ImagingPlan> BuildPlanAsync(
        string targetQuery,
        string userPreferences,
        SetupContext setup,
        IReadOnlyList<TargetSchedulerTemplateInfo> templates,
        IntegrationAmbition ambition,
        FramingSnapshot? framing,
        ILLMProvider provider,
        CancellationToken cancellationToken) {

        var target = await resolver.ResolveAsync(targetQuery, cancellationToken).ConfigureAwait(false);
        var system = """
You are the acquisition-planning component of NightSage, a N.I.N.A. astrophotography plugin.
Return JSON only. Do not wrap it in Markdown.
The target coordinates have already been resolved authoritatively; do not invent or replace them.
Choose an astrophotography strategy for the exact active equipment supplied.
When filters are listed, use only those exact filter names in the exposures array.
If no filters are listed, use filter "OSC" for a color-camera strategy.
Target Scheduler exposure templates, when supplied, are the user's acquisition source of truth.
For routine exposures, strongly prefer an existing same-filter template and its default exposure duration.
Set preferredTemplate to the exact existing template name you want NightSage to use.
You may request a different exposure duration when it has a real imaging purpose, especially HDR/highlights/bright stars.
For such a special exposure, set preferredTemplate to the same-filter existing template that should be cloned as the technical base.
NightSage will preserve the selected base template's gain, offset, binning, readout, twilight, dithering, humidity and moon-avoidance settings and change only exposure duration.
Only propose low-level camera/moon settings from scratch when no same-filter template exists.
The integration ambition is not a duration bucket. Never add unnecessary hours just to match a preset.
When an approved N.I.N.A. framing is supplied, do not change its geometry. The exposure totals you return are PER PANEL.
For a mosaic, the integration ambition applies to the TOTAL project time across all panels, not to each panel separately.
""";

        var prompt = BuildPlanPrompt(target, setup, templates, userPreferences, ambition, framing);
        var raw = await provider.CompleteJsonAsync(system, prompt, cancellationToken).ConfigureAwait(false);
        using var doc = JsonPayload.ParseObject(raw);
        var plan = ParsePlan(doc.RootElement, target, setup, ambition, framing);
        var validated = validator.Validate(plan, setup);
        var warning = IntegrationAmbitionPolicy.OverrunWarning(ambition, validated.TotalIntegrationMinutes / 60.0);
        if (!string.IsNullOrWhiteSpace(warning)) validated.Warnings.Add(warning);
        return validated;
    }

    private static string BuildPlanPrompt(
        ResolvedTarget target,
        SetupContext setup,
        IReadOnlyList<TargetSchedulerTemplateInfo> templates,
        string preferences,
        IntegrationAmbition ambition,
        FramingSnapshot? framing) {

        var filters = setup.Filters.Count == 0 ? "(none configured)" : string.Join(", ", setup.Filters);
        var templateText = templates.Count == 0
            ? "(none available / Target Scheduler not loaded)"
            : string.Join(Environment.NewLine, templates.Select(t =>
                $"- {t.Name}: filter={t.FilterName}, exposure={t.DefaultExposure:0.###}s, gain={t.Gain}, offset={t.Offset}, bin={t.Binning}, readout={t.ReadoutMode}, moonAvoidance={t.MoonAvoidanceEnabled}, moonSep={t.MoonAvoidanceSeparation:0.#}°, moonWidth={t.MoonAvoidanceWidth}"));

        var framingText = framing == null
            ? "(none — plan as a single target centered on the resolved coordinates)"
            : string.Join(Environment.NewLine, new[] {
                $"- source: {framing.Source}",
                $"- center J2000 RA: {framing.CenterRaHours.ToString("0.######", CultureInfo.InvariantCulture)} hours",
                $"- center J2000 Dec: {framing.CenterDecDeg.ToString("0.######", CultureInfo.InvariantCulture)} degrees",
                $"- grid: {framing.GridDisplay}",
                $"- panel count: {framing.PanelCount}",
                $"- overlap: {framing.OverlapDisplay}",
                "- panels:",
                string.Join(Environment.NewLine, framing.Panels.Select(p =>
                    $"  - {p.Name}: RA={p.RaHours.ToString("0.######", CultureInfo.InvariantCulture)}h, Dec={p.DecDeg.ToString("0.######", CultureInfo.InvariantCulture)}deg, rotation={p.RotationDegrees.ToString("0.###", CultureInfo.InvariantCulture)}deg"))
            });

        return $$"""
Target:
- query: {{target.Query}}
- canonical name: {{target.CanonicalName}}
- catalog J2000 RA: {{target.RaHours.ToString("0.######", CultureInfo.InvariantCulture)}} hours
- catalog J2000 Dec: {{target.DecDeg.ToString("0.######", CultureInfo.InvariantCulture)}} degrees

Integration ambition: {{ambition}}
Ambition guidance: {{IntegrationAmbitionPolicy.PlanningGuidance(ambition)}}

Active N.I.N.A. setup:
- profile: {{setup.ProfileName}}
- telescope: {{setup.TelescopeName}}
- focal length: {{setup.FocalLengthMm:0.##}} mm
- focal ratio: f/{{setup.FocalRatio:0.##}}
- camera: {{setup.CameraName}}
- pixel size: {{setup.PixelSizeMicrons:0.###}} µm
- sensor: {{setup.SensorWidthPixels}} x {{setup.SensorHeightPixels}} px
- field of view: {{setup.FieldWidthDeg:0.###}} x {{setup.FieldHeightDeg:0.###}} deg
- image scale: {{setup.ImageScaleArcsecPerPixel:0.###}} arcsec/px
- Bayer pattern: {{setup.BayerPattern}}
- configured filter names (exact): {{filters}}
- site: lat {{setup.LatitudeDeg:0.####}}, lon {{setup.LongitudeDeg:0.####}}, elevation {{setup.ElevationM:0}} m

Existing Target Scheduler exposure templates:
{{templateText}}

Approved N.I.N.A. framing:
{{framingText}}

Optional user planning instructions:
{{(string.IsNullOrWhiteSpace(preferences) ? "(none)" : preferences.Trim())}}

If framing is a mosaic, totalMinutes in each exposure is the desired integration PER PANEL.
Remember that project total integration equals the sum of per-panel exposure totals multiplied by the panel count.
Do not increase exposure time merely because multiple panels exist; balance the result quality against the selected integration ambition for the whole project.

Return exactly this JSON shape:
{
  "targetType": "emission nebula|reflection nebula|dark nebula|galaxy|planetary nebula|supernova remnant|cluster|other",
  "angularWidthArcmin": number,
  "angularHeightArcmin": number,
  "rotationDegrees": number,
  "minimumAltitudeDegrees": number,
  "minimumSessionMinutes": integer,
  "projectPriority": "Low|Normal|High",
  "filterSwitchFrequency": integer,
  "ditherEvery": integer,
  "smartExposureOrder": boolean,
  "strategySummary": "short explanation",
  "exposures": [
    {
      "filter": "exact N.I.N.A. filter name or OSC",
      "subSeconds": number,
      "totalMinutes": number,
      "preferredTemplate": "exact existing template name, or empty string",
      "binning": integer,
      "gain": integer or null,
      "offset": integer or null,
      "readoutMode": integer or null,
      "moonAvoidanceEnabled": boolean,
      "moonSeparationDeg": number,
      "moonWidthDays": integer,
      "moonRelaxScale": number,
      "moonDownEnabled": boolean,
      "twilight": "Nighttime|Astronomical|Nautical|Civil"
    }
  ]
}
""";
    }

    private static ImagingPlan ParsePlan(
        JsonElement root,
        ResolvedTarget target,
        SetupContext setup,
        IntegrationAmbition ambition,
        FramingSnapshot? framing) {

        var plan = new ImagingPlan {
            TargetName = string.IsNullOrWhiteSpace(target.CanonicalName) ? target.Query : target.CanonicalName,
            TargetType = JsonPayload.String(root, "targetType", "other"),
            Ambition = ambition,
            RaHours = framing?.CenterRaHours ?? target.RaHours,
            DecDeg = framing?.CenterDecDeg ?? target.DecDeg,
            AngularWidthArcmin = JsonPayload.Double(root, "angularWidthArcmin"),
            AngularHeightArcmin = JsonPayload.Double(root, "angularHeightArcmin"),
            RotationDegrees = framing?.PrimaryRotationDegrees ?? JsonPayload.Double(root, "rotationDegrees"),
            MinimumAltitudeDegrees = JsonPayload.Double(root, "minimumAltitudeDegrees", 30),
            MinimumSessionMinutes = JsonPayload.Int(root, "minimumSessionMinutes", 60),
            ProjectPriority = JsonPayload.String(root, "projectPriority", "Normal"),
            FilterSwitchFrequency = JsonPayload.Int(root, "filterSwitchFrequency", 0),
            DitherEvery = JsonPayload.Int(root, "ditherEvery", 1),
            SmartExposureOrder = JsonPayload.Bool(root, "smartExposureOrder", true),
            StrategySummary = JsonPayload.String(root, "strategySummary"),
            Framing = framing
        };

        if (root.TryGetProperty("exposures", out var exposures) && exposures.ValueKind == JsonValueKind.Array) {
            foreach (var e in exposures.EnumerateArray()) {
                int? nullableInt(string name) {
                    if (!e.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null) return null;
                    return p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v) ? v : null;
                }
                plan.Exposures.Add(new ExposureRecommendation {
                    Filter = JsonPayload.String(e, "filter", setup.Filters.Count == 1 ? setup.Filters[0] : "OSC"),
                    SubSeconds = JsonPayload.Double(e, "subSeconds", 120),
                    TotalMinutes = JsonPayload.Double(e, "totalMinutes", 120),
                    PreferredTemplateName = JsonPayload.String(e, "preferredTemplate"),
                    Binning = JsonPayload.Int(e, "binning", 1),
                    Gain = nullableInt("gain"), Offset = nullableInt("offset"), ReadoutMode = nullableInt("readoutMode"),
                    MoonAvoidanceEnabled = JsonPayload.Bool(e, "moonAvoidanceEnabled"),
                    MoonSeparationDeg = JsonPayload.Double(e, "moonSeparationDeg", 60),
                    MoonWidthDays = JsonPayload.Int(e, "moonWidthDays", 7),
                    MoonRelaxScale = JsonPayload.Double(e, "moonRelaxScale", 0),
                    MoonDownEnabled = JsonPayload.Bool(e, "moonDownEnabled"),
                    Twilight = JsonPayload.String(e, "twilight", "Nighttime")
                });
            }
        }
        return plan;
    }
}
