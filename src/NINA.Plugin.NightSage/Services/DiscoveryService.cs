using NINA.Plugin.NightSage.Infrastructure;
using NINA.Plugin.NightSage.Models;
using NINA.Plugin.NightSage.Providers;
using System.Globalization;
using System.Text.Json;

namespace NINA.Plugin.NightSage.Services;

public sealed class DiscoveryService {
    private readonly ITargetResolver resolver;

    public DiscoveryService(ITargetResolver? resolver = null) {
        this.resolver = resolver ?? new SesameTargetResolver();
    }

    public async Task<DiscoveryResult> DiscoverAsync(
        SetupContext setup,
        IReadOnlyCollection<ExistingTargetInfo> existingTargets,
        ILLMProvider provider,
        int days,
        double minimumAltitude,
        CancellationToken cancellationToken) {

        var system = """
You are the target-discovery component of NightSage, a N.I.N.A. astrophotography plugin.
Return JSON only, with no Markdown.
Recommend targets that are genuinely plausible from the supplied site during the next seven days and appropriate to the exact field of view and filters.
The plugin will independently resolve coordinates and calculate visibility, so use well-known resolvable catalog names (Messier, NGC, IC, Sharpless, Abell, etc.).
Return one candidate for each requested category. Do not repeat the same physical object in multiple categories.
Prefer unfinished existing Target Scheduler targets when they are a strong fit, but do not force them.
""";

        var prompt = BuildPrompt(setup, existingTargets, days, minimumAltitude);
        var raw = await provider.CompleteJsonAsync(system, prompt, cancellationToken).ConfigureAwait(false);
        using var doc = JsonPayload.ParseObject(raw);

        var candidates = new List<TargetCandidate>();
        if (doc.RootElement.TryGetProperty("candidates", out var items) && items.ValueKind == JsonValueKind.Array) {
            foreach (var item in items.EnumerateArray()) {
                cancellationToken.ThrowIfCancellationRequested();
                var name = JsonPayload.String(item, "name").Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                ResolvedTarget resolved;
                try {
                    resolved = await resolver.ResolveAsync(name, cancellationToken).ConfigureAwait(false);
                } catch {
                    continue;
                }

                var candidate = new TargetCandidate {
                    Category = JsonPayload.String(item, "category", "Other"),
                    Name = string.IsNullOrWhiteSpace(resolved.CanonicalName) ? name : resolved.CanonicalName,
                    TargetType = JsonPayload.String(item, "targetType", "other"),
                    RaHours = resolved.RaHours,
                    DecDeg = resolved.DecDeg,
                    AngularWidthArcmin = JsonPayload.Double(item, "angularWidthArcmin"),
                    AngularHeightArcmin = JsonPayload.Double(item, "angularHeightArcmin"),
                    Reason = JsonPayload.String(item, "reason"),
                    ModelScore = Math.Clamp(JsonPayload.Double(item, "modelScore", 70), 0, 100)
                };

                candidate.Visibility = AstronomyMath.VisibilityNextDays(
                    candidate.RaHours, candidate.DecDeg,
                    setup.LatitudeDeg, setup.LongitudeDeg,
                    minimumAltitude, days);

                candidate.AlreadyInTargetScheduler = existingTargets.Any(x =>
                    NamesEquivalent(x.TargetName, candidate.Name) || NamesEquivalent(x.TargetName, name));

                candidate.DeterministicScore = AstronomyMath.CandidateScore(setup, candidate, minimumAltitude);
                candidate.TotalScore = candidate.DeterministicScore;
                candidates.Add(candidate);
            }
        }

        var selected = candidates
            .Where(x => x.Visibility.DarkHoursAboveMinimum >= 0.5)
            .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.TotalScore).First())
            .OrderByDescending(x => x.TotalScore)
            .ToList();

        var result = new DiscoveryResult();
        foreach (var c in selected) result.Candidates.Add(c);
        return result;
    }

    private static string BuildPrompt(
        SetupContext setup,
        IReadOnlyCollection<ExistingTargetInfo> existingTargets,
        int days,
        double minimumAltitude) {

        var existing = existingTargets.Count == 0
            ? "(none)"
            : string.Join("\n", existingTargets.Take(30).Select(x =>
                $"- {x.TargetName} / project {x.ProjectName} / {x.PercentComplete:0}% complete / active={x.ProjectActive}"));

        var filters = setup.Filters.Count == 0 ? "(none; color camera/no wheel may be in use)" : string.Join(", ", setup.Filters);

        return $"""
Current UTC date: {DateTime.UtcNow:yyyy-MM-dd}
Discovery horizon: next {days} days
Minimum useful target altitude: {minimumAltitude:0} deg

Site:
- latitude {setup.LatitudeDeg.ToString("0.####", CultureInfo.InvariantCulture)} deg
- longitude {setup.LongitudeDeg.ToString("0.####", CultureInfo.InvariantCulture)} deg
- elevation {setup.ElevationM:0} m

Active setup:
- telescope {setup.TelescopeName}
- focal length {setup.FocalLengthMm:0} mm, f/{setup.FocalRatio:0.0}
- camera {setup.CameraName}
- field {setup.FieldWidthDeg:0.###} x {setup.FieldHeightDeg:0.###} deg
- image scale {setup.ImageScaleArcsecPerPixel:0.###} arcsec/px
- exact configured filters: {filters}

Current Target Scheduler targets:
{existing}

Return exactly:
{{
  "candidates": [
    {{
      "category": "Emission nebula",
      "name": "catalog name",
      "targetType": "short type",
      "angularWidthArcmin": number,
      "angularHeightArcmin": number,
      "reason": "why this setup and this week suit it",
      "modelScore": number from 0 to 100
    }}
  ]
}}

Return exactly one object in candidates for each category:
1. Emission nebula / HII region
2. Reflection or dark nebula
3. Galaxy
4. Planetary nebula
5. Supernova remnant / WR shell
6. Star cluster or broadband star field

Do not omit a category merely because it is not the globally best choice; give the strongest realistic candidate for that category.
""";
    }

    private static bool NamesEquivalent(string a, string b) {
        static string Normalize(string s) => new string((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        var x = Normalize(a);
        var y = Normalize(b);
        return x.Length > 0 && (x == y || x.Contains(y) || y.Contains(x));
    }
}
