using NINA.Plugin.NightSage.Models;
using NUnit.Framework;

namespace NINA.Plugin.NightSage.Tests;

[TestFixture]
public class IntegrationSummaryTests {
    [Test]
    public void Exposure_UsesActualDesiredCountForIntegration() {
        var exposure = new ExposureRecommendation { Filter = "H", SubSeconds = 600, DesiredCount = 31 };
        Assert.That(exposure.PlannedIntegrationMinutes, Is.EqualTo(310));
        Assert.That(exposure.IntegrationDisplay, Is.EqualTo("5h 10m"));
    }

    [Test]
    public void Plan_AggregatesHdrRowsByFilterAndTotal() {
        var plan = new ImagingPlan {
            Exposures = new List<ExposureRecommendation> {
                new() { Filter = "H", SubSeconds = 600, DesiredCount = 30 },
                new() { Filter = "H", SubSeconds = 60, DesiredCount = 60 },
                new() { Filter = "O", SubSeconds = 600, DesiredCount = 24 }
            }
        };

        Assert.That(plan.FilterIntegrationSummary, Does.Contain("H: 6h 00m"));
        Assert.That(plan.FilterIntegrationSummary, Does.Contain("O: 4h 00m"));
        Assert.That(plan.TotalIntegrationDisplay, Is.EqualTo("10h 00m"));
    }
}
