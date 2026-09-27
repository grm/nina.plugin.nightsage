using NINA.Plugin.NightSage.Models;
using NUnit.Framework;

namespace NINA.Plugin.NightSage.Tests;

[TestFixture]
public class IntegrationAmbitionTests {
    [Test]
    public void Balanced_DoesNotPenalizeShortTargets() {
        Assert.That(IntegrationAmbitionPolicy.DiscoveryScoreAdjustment(IntegrationAmbition.Balanced, 6), Is.EqualTo(0));
        Assert.That(IntegrationAmbitionPolicy.DiscoveryScoreAdjustment(IntegrationAmbition.Balanced, 10), Is.EqualTo(0));
        Assert.That(IntegrationAmbitionPolicy.DiscoveryScoreAdjustment(IntegrationAmbition.Balanced, 20), Is.EqualTo(0));
    }

    [Test]
    public void Deep_NeverRewardsOrPenalizesDuration() {
        Assert.That(IntegrationAmbitionPolicy.DiscoveryScoreAdjustment(IntegrationAmbition.Deep, 5), Is.EqualTo(0));
        Assert.That(IntegrationAmbitionPolicy.DiscoveryScoreAdjustment(IntegrationAmbition.Deep, 50), Is.EqualTo(0));
    }

    [Test]
    public void Quick_OnlyPenalizesAboveSoftTarget() {
        Assert.That(IntegrationAmbitionPolicy.DiscoveryScoreAdjustment(IntegrationAmbition.Quick, 8), Is.EqualTo(0));
        Assert.That(IntegrationAmbitionPolicy.DiscoveryScoreAdjustment(IntegrationAmbition.Quick, 15), Is.LessThan(0));
    }

    [Test]
    public void Balanced_OnlyPenalizesLongProjectsAboveSoftRange() {
        Assert.That(IntegrationAmbitionPolicy.DiscoveryScoreAdjustment(IntegrationAmbition.Balanced, 18), Is.EqualTo(0));
        Assert.That(IntegrationAmbitionPolicy.DiscoveryScoreAdjustment(IntegrationAmbition.Balanced, 30), Is.LessThan(0));
    }
}
