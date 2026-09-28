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

    [Test]
    public void Validator_RejectsNonFiniteRotation() {
        var setup = new SetupContext { Filters = new List<string> { "Ha" } };
        var plan = ValidPlan();
        plan.RotationDegrees = double.NaN;

        Assert.Throws<InvalidOperationException>(() => new PlanValidator().Validate(plan, setup));
    }

    [Test]
    public void Validator_RejectsNonFiniteExposureDuration() {
        var setup = new SetupContext { Filters = new List<string> { "Ha" } };
        var plan = ValidPlan();
        plan.Exposures[0].SubSeconds = double.PositiveInfinity;

        Assert.Throws<InvalidOperationException>(() => new PlanValidator().Validate(plan, setup));
    }

    [Test]
    public void Validator_RejectsNonFiniteMosaicPanelRotation() {
        var setup = new SetupContext { Filters = new List<string> { "Ha" } };
        var plan = ValidPlan();
        plan.Framing = new FramingSnapshot {
            CenterRaHours = 5,
            CenterDecDeg = 20,
            Panels = new List<FramingPanel> {
                new() { Index = 1, RaHours = 5, DecDeg = 20, RotationDegrees = double.NaN }
            }
        };

        Assert.Throws<InvalidOperationException>(() => new PlanValidator().Validate(plan, setup));
    }

    private static ImagingPlan ValidPlan() => new() {
        TargetName = "Test",
        RaHours = 5,
        DecDeg = 20,
        RotationDegrees = 0,
        MinimumAltitudeDegrees = 30,
        Exposures = new List<ExposureRecommendation> {
            new() { Filter = "Ha", SubSeconds = 300, TotalMinutes = 60 }
        }
    };
}
