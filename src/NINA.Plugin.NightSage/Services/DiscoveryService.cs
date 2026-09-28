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

    public Task<DiscoveryResult> DiscoverAsync(
        SetupContext setup,
        IReadOnlyCollection<ExistingTargetInfo> existingTargets,
        ILLMProvider provider,
        int days,
        double minimumAltitude,
        CancellationToken cancellationToken) =>
        DiscoverAsync(
            setup, existingTargets, provider, days, minimumAltitude, IntegrationAmbition.Balanced,
            DiscoveryCategoryCatalog.All.Select(x => x.Key).ToArray(), 12, cancellationToken);

    public Task<DiscoveryResult> DiscoverAsync(
        SetupContext setup,
        IReadOnlyCollection<ExistingTargetInfo> existingTargets,
        ILLMProvider provider,
        int days,
        double minimumAltitude,
        IntegrationAmbition ambition,
        CancellationToken cancellationToken) =>
        DiscoverAsync(
            setup, existingTargets, provider, days, minimumAltitude, ambition,
            DiscoveryCategoryCatalog.All.Select(x => x.Key).ToArray(), 12, cancellationToken);

    public async Task<DiscoveryResult> DiscoverAsync(
        SetupContext setup,
        IReadOnlyCollection<ExistingTargetInfo> existingTargets,
        ILLMProvider provider,
        int days,
        double minimumAltitude,
        IntegrationAmbition ambition,
        IReadOnlyCollection<string> selectedCategoryKeys,
        int resultLimit,
        CancellationToken cancellationToken) {

        var selectedCategories = selectedCategoryKeys
            .Select(DiscoveryCategoryCatalog.Find)
            .Where(x => x != null)
            .Cast<DiscoveryCategoryDefinition>()
            .DistinctBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedCategories.Count == 0)
            throw new InvalidOperationException("Select at least one target type before running discovery.");

        resultLimit = Math.Clamp(resultLimit, 1, 30);

        var system = """
You are the target-discovery component of NightSage, a N.I.N.A. astrophotography plugin.
Return JSON only, with no Markdown.
Recommend targets that are genuinely plausible from the supplied site during the next seven days and appropriate to the exact field of view and filters.
The plugin independently resolves coordinates and calculates visibility, so use well-known resolvable catalog names (Messier, NGC, IC, Sharpless, Abell, etc.).
Only return the requested category keys.
Do not repeat the same physical object under aliases or in multiple categories.
Prefer unfinished existing Target Scheduler targets when they are a strong fit, but do not force them.
Estimate a realistic total integration time for the intended result with this exact setup.
The integration ambition is a tolerance for project length, not a duration bucket. Never make a target rank higher merely because it needs more hours.
Return multiple alternatives when the result limit allows it. Cover the selected categories as evenly as practical, then use remaining slots for the strongest additional targets.
""";

        var discoveryStartUtc = DateTime.UtcNow;
        var prompt = BuildPrompt(setup, existingTargets, days, minimumAltitude, ambition, selectedCategories, resultLimit, discoveryStartUtc);
        var raw = await provider.CompleteJsonAsync(system, prompt, cancellationToken).ConfigureAwait(false);
        using var doc = JsonPayload.ParseObject(raw);

        var allowedKeys = selectedCategories.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<TargetCandidate>();

        if (doc.RootElement.TryGetProperty("candidates", out var items) && items.ValueKind == JsonValueKind.Array) {
            foreach (var item in items.EnumerateArray()) {
                cancellationToken.ThrowIfCancellationRequested();

                var categoryKey = JsonPayload.String(item, "categoryKey").Trim();
                if (!allowedKeys.Contains(categoryKey)) continue;
                var category = DiscoveryCategoryCatalog.Find(categoryKey);
                if (category == null) continue;

                var name = JsonPayload.String(item, "name").Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                ResolvedTarget resolved;
                try {
                    resolved = await resolver.ResolveAsync(name, cancellationToken).ConfigureAwait(false);
                } catch {
                    continue;
                }

                var candidate = new TargetCandidate {
                    Category = category.DisplayName,
                    Name = string.IsNullOrWhiteSpace(resolved.CanonicalName) ? name : resolved.CanonicalName,
                    TargetType = JsonPayload.String(item, "targetType", "other"),
                    RaHours = resolved.RaHours,
                    DecDeg = resolved.DecDeg,
                    AngularWidthArcmin = JsonPayload.Double(item, "angularWidthArcmin"),
                    AngularHeightArcmin = JsonPayload.Double(item, "angularHeightArcmin"),
                    EstimatedIntegrationHours = Math.Max(0, JsonPayload.Double(item, "estimatedIntegrationHours", 0)),
                    Reason = JsonPayload.String(item, "reason"),
                    ModelScore = Math.Clamp(JsonPayload.Double(item, "modelScore", 70), 0, 100)
                };

                candidate.Visibility = AstronomyMath.VisibilityNextDays(
                    candidate.RaHours, candidate.DecDeg,
                    setup.LatitudeDeg, setup.LongitudeDeg,
                    setup.ElevationM,
                    minimumAltitude, days, discoveryStartUtc);

                candidate.AlreadyInTargetScheduler = existingTargets.Any(x =>
                    NamesEquivalent(x.TargetName, candidate.Name) || NamesEquivalent(x.TargetName, name));

                candidate.DeterministicScore = AstronomyMath.CandidateScore(setup, candidate, minimumAltitude);
                var ambitionAdjustment = IntegrationAmbitionPolicy.DiscoveryScoreAdjustment(ambition, candidate.EstimatedIntegrationHours);
                candidate.TotalScore = Math.Clamp(candidate.DeterministicScore + ambitionAdjustment, 0, 100);
                candidates.Add(candidate);
            }
        }

        var valid = candidates
            .Where(x => x.Visibility.DarkHoursAboveMinimum >= 0.5)
            .GroupBy(x => NormalizeName(x.Name))
            .Select(g => g.OrderByDescending(x => x.TotalScore).First())
            .ToList();

        var selected = new List<TargetCandidate>();

        // First pass: preserve category diversity.
        foreach (var best in valid
                     .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.OrderByDescending(x => x.TotalScore).First())
                     .OrderByDescending(x => x.TotalScore)) {
            if (selected.Count >= resultLimit) break;
            selected.Add(best);
        }

        // Second pass: fill remaining slots with the strongest alternatives.
        foreach (var candidate in valid
                     .Where(x => !selected.Contains(x))
                     .OrderByDescending(x => x.TotalScore)) {
            if (selected.Count >= resultLimit) break;
            selected.Add(candidate);
        }

        selected = selected.OrderByDescending(x => x.TotalScore).ToList();

        var result = new DiscoveryResult();
        foreach (var c in selected) result.Candidates.Add(c);
        return result;
    }

    private static string BuildPrompt(
        SetupContext setup,
        IReadOnlyCollection<ExistingTargetInfo> existingTargets,
        int days,
        double minimumAltitude,
        IntegrationAmbition ambition,
        IReadOnlyList<DiscoveryCategoryDefinition> categories,
        int resultLimit,
        DateTime discoveryStartUtc) {

        var existing = existingTargets.Count == 0
            ? "(none)"
            : string.Join("\n", existingTargets.Take(30).Select(x =>
                $"- {x.TargetName} / project {x.ProjectName} / {x.PercentComplete:0}% complete / active={x.ProjectActive}"));

        var filters = setup.Filters.Count == 0 ? "(none; color camera/no wheel may be in use)" : string.Join(", ", setup.Filters);
        var categoryList = string.Join("\n", categories.Select(x => $"- {x.Key}: {x.PromptName}"));

        return $$"""
Current UTC date/time: {{discoveryStartUtc:yyyy-MM-dd HH:mm:ss}}Z
Discovery horizon: next {{days}} days, starting now
Minimum useful target altitude: {{minimumAltitude:0}} deg
Integration ambition: {{ambition}}
Ambition guidance: {{IntegrationAmbitionPolicy.DiscoveryGuidance(ambition)}}
Maximum results requested: {{resultLimit}}

Requested target categories:
{{categoryList}}

Site:
- latitude {{setup.LatitudeDeg.ToString("0.####", CultureInfo.InvariantCulture)}} deg
- longitude {{setup.LongitudeDeg.ToString("0.####", CultureInfo.InvariantCulture)}} deg
- elevation {{setup.ElevationM:0}} m

Active setup:
- telescope {{setup.TelescopeName}}
- focal length {{setup.FocalLengthMm:0}} mm, f/{{setup.FocalRatio:0.0}}
- camera {{setup.CameraName}}
- field {{setup.FieldWidthDeg:0.###}} x {{setup.FieldHeightDeg:0.###}} deg
- image scale {{setup.ImageScaleArcsecPerPixel:0.###}} arcsec/px
- exact configured filters: {{filters}}

Current Target Scheduler targets:
{{existing}}

Return exactly:
{
  "candidates": [
    {
      "categoryKey": "one exact requested category key",
      "name": "resolvable catalog name",
      "targetType": "short type",
      "angularWidthArcmin": number,
      "angularHeightArcmin": number,
      "estimatedIntegrationHours": number,
      "reason": "why this setup, this week and this integration ambition suit it",
      "modelScore": number from 0 to 100
    }
  ]
}

Return at most {{resultLimit}} candidates total.
Try to include at least one strong candidate from every requested category when the result limit permits.
If more slots remain, add strong alternatives from any requested category.
Short targets remain eligible in Balanced and Deep. Do not stretch estimated integration to match the selected ambition.
""";
    }

    private static bool NamesEquivalent(string a, string b) {
        var x = NormalizeName(a);
        var y = NormalizeName(b);
        return x.Length > 0 && (x == y || x.Contains(y) || y.Contains(x));
    }

    private static string NormalizeName(string s) =>
        new string((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
