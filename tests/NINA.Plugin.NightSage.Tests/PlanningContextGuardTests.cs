using NINA.Plugin.NightSage.Models;
using NINA.Plugin.NightSage.Services;
using NUnit.Framework;

namespace NINA.Plugin.NightSage.Tests;

[TestFixture]
public class PlanningContextGuardTests {
    [Test]
    public void ProfileChange_RejectsPendingResult() {
        Assert.Throws<OperationCanceledException>(() =>
            PlanningContextGuard.EnsureProfileUnchanged("profile-a", "profile-b"));
    }

    [Test]
    public void FramingChange_RejectsPendingRecalculation() {
        Assert.Throws<OperationCanceledException>(() =>
            PlanningContextGuard.EnsureFramingUnchanged("framing-a", "framing-b"));
    }

    [Test]
    public void Creation_RejectsPlanFromDifferentProfile() {
        var plan = new ImagingPlan { SourceProfileId = "profile-a", IsValidated = true };
        Assert.Throws<InvalidOperationException>(() =>
            PlanningContextGuard.EnsurePlanMatchesProfile(plan, "profile-b"));
    }

    [Test]
    public void MatchingContext_IsAccepted() {
        var plan = new ImagingPlan { SourceProfileId = "profile-a", IsValidated = true };
        Assert.DoesNotThrow(() => PlanningContextGuard.EnsureProfileUnchanged("profile-a", "profile-a"));
        Assert.DoesNotThrow(() => PlanningContextGuard.EnsureFramingUnchanged("same", "same"));
        Assert.DoesNotThrow(() => PlanningContextGuard.EnsurePlanMatchesProfile(plan, "profile-a"));
    }
}
