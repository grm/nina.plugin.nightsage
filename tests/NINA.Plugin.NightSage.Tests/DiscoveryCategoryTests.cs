using NINA.Plugin.NightSage.Infrastructure;
using NINA.Plugin.NightSage.Services;
using NUnit.Framework;

namespace NINA.Plugin.NightSage.Tests;

[TestFixture]
public class DiscoveryCategoryTests {
    [Test]
    public void SelectedKeys_DefaultsToAllSixCategories() {
        var keys = DiscoveryCategoryCatalog.SelectedKeys(new NightSageSettings());
        Assert.That(keys, Has.Count.EqualTo(6));
    }

    [Test]
    public void SelectedKeys_RespectsDisabledCategories() {
        var settings = new NightSageSettings {
            DiscoverGalaxies = false,
            DiscoverPlanetaryNebulae = false
        };
        var keys = DiscoveryCategoryCatalog.SelectedKeys(settings);
        Assert.That(keys, Does.Not.Contain("galaxy"));
        Assert.That(keys, Does.Not.Contain("planetary"));
        Assert.That(keys, Has.Count.EqualTo(4));
    }
}
