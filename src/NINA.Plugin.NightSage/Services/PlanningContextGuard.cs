using NINA.Plugin.NightSage.Models;

namespace NINA.Plugin.NightSage.Services;

public static class PlanningContextGuard {
    public static void EnsureProfileUnchanged(string expectedProfileId, string currentProfileId) {
        if (!string.Equals(expectedProfileId, currentProfileId, StringComparison.Ordinal))
            throw new OperationCanceledException("Active N.I.N.A. profile changed while the operation was running; the outdated result was discarded.");
    }

    public static void EnsureFramingUnchanged(string expectedFingerprint, string currentFingerprint) {
        if (!string.Equals(expectedFingerprint, currentFingerprint, StringComparison.Ordinal))
            throw new OperationCanceledException("Framing changed while the acquisition plan was being recalculated; the outdated result was discarded.");
    }

    public static void EnsurePlanMatchesProfile(ImagingPlan plan, string activeProfileId) {
        if (string.IsNullOrWhiteSpace(plan.SourceProfileId) ||
            !string.Equals(plan.SourceProfileId, activeProfileId, StringComparison.Ordinal)) {
            throw new InvalidOperationException("This NightSage plan was built for a different N.I.N.A. profile. Rebuild the plan for the active profile before creating it in Target Scheduler.");
        }
    }
}
