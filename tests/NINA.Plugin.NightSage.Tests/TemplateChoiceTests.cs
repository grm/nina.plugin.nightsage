using NINA.Plugin.NightSage.Models;
using NINA.Plugin.NightSage.Services;
using NUnit.Framework;

namespace NINA.Plugin.NightSage.Tests;

[TestFixture]
public class TemplateChoiceTests {
    [Test]
    public void Build_PrefersExactExistingTemplate() {
        var exp = new ExposureRecommendation { Filter = "H", SubSeconds = 600, Binning = 2 };
        var plan = new ImagingPlan { Exposures = new List<ExposureRecommendation> { exp } };
        var templates = new List<TargetSchedulerTemplateInfo> {
            new() { Id = 1, Name = "H - 300", FilterName = "H", DefaultExposure = 300, Binning = 2 },
            new() { Id = 2, Name = "H - 600", FilterName = "H", DefaultExposure = 600, Binning = 2 }
        };
        var choice = TemplateChoiceService.Build(plan, templates).Single();
        Assert.That(choice.SelectedTemplate?.Id, Is.EqualTo(2));
        Assert.That(choice.UsesExistingUnchanged, Is.True);
    }

    [Test]
    public void Build_HonorsPreferredBaseForDerivedExposure() {
        var exp = new ExposureRecommendation { Filter = "H", SubSeconds = 15, PreferredTemplateName = "H - 600" };
        var plan = new ImagingPlan { Exposures = new List<ExposureRecommendation> { exp } };
        var templates = new List<TargetSchedulerTemplateInfo> {
            new() { Id = 1, Name = "H - 60", FilterName = "H", DefaultExposure = 60 },
            new() { Id = 2, Name = "H - 600", FilterName = "H", DefaultExposure = 600 }
        };
        var choice = TemplateChoiceService.Build(plan, templates).Single();
        Assert.That(choice.SelectedTemplate?.Id, Is.EqualTo(2));
        Assert.That(choice.IsDerived, Is.True);
    }

    [Test]
    public void Validator_KeepsHdrDurationsForSameFilter() {
        var setup = new SetupContext { Filters = new List<string> { "H" } };
        var plan = new ImagingPlan {
            RaHours = 5, DecDeg = 20,
            Exposures = new List<ExposureRecommendation> {
                new() { Filter = "H", SubSeconds = 600, TotalMinutes = 120 },
                new() { Filter = "H", SubSeconds = 60, TotalMinutes = 20 },
                new() { Filter = "H", SubSeconds = 15, TotalMinutes = 5 }
            }
        };
        var result = new PlanValidator().Validate(plan, setup);
        Assert.That(result.Exposures, Has.Count.EqualTo(3));
    }
}
