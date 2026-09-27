using NINA.Plugin.NightSage.Models;
using NINA.Plugin.NightSage.Services;
using NUnit.Framework;

namespace NINA.Plugin.NightSage.Tests;

[TestFixture]
public class PlanValidatorTests {
    [Test]
    public void FilterMatcher_MapsCommonAliases() {
        var filters = new[] { "Ha", "OIII", "SII", "R", "G", "B" };
        Assert.That(FilterMatcher.Match("H-alpha", filters), Is.EqualTo("Ha"));
        Assert.That(FilterMatcher.Match("Oxygen III", filters), Is.EqualTo("OIII"));
        Assert.That(FilterMatcher.Match("red", filters), Is.EqualTo("R"));
    }

    [Test]
    public void Validator_DropsUnavailableFilter_AndComputesCount() {
        var setup = new SetupContext {
            Filters = new List<string> { "Ha", "OIII" },
            DefaultGain = 100,
            DefaultOffset = 20
        };
        var plan = new ImagingPlan {
            TargetName = "Test",
            RaHours = 5,
            DecDeg = 20,
            Exposures = new List<ExposureRecommendation> {
                new() { Filter = "Ha", SubSeconds = 300, TotalMinutes = 60 },
                new() { Filter = "SII", SubSeconds = 300, TotalMinutes = 60 }
            }
        };

        var result = new PlanValidator().Validate(plan, setup);
        Assert.That(result.Exposures, Has.Count.EqualTo(1));
        Assert.That(result.Exposures[0].DesiredCount, Is.EqualTo(12));
        Assert.That(result.Warnings, Is.Not.Empty);
    }
}
