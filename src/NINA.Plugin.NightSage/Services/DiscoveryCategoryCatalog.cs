using NINA.Plugin.NightSage.Infrastructure;

namespace NINA.Plugin.NightSage.Services;

public sealed record DiscoveryCategoryDefinition(string Key, string DisplayName, string PromptName);

public static class DiscoveryCategoryCatalog {
    public static IReadOnlyList<DiscoveryCategoryDefinition> All { get; } = new[] {
        new DiscoveryCategoryDefinition("emission", "Emission / HII", "Emission nebula / HII region"),
        new DiscoveryCategoryDefinition("reflection_dark", "Reflection / dark nebula", "Reflection or dark nebula"),
        new DiscoveryCategoryDefinition("galaxy", "Galaxy", "Galaxy"),
        new DiscoveryCategoryDefinition("planetary", "Planetary nebula", "Planetary nebula"),
        new DiscoveryCategoryDefinition("snr_wr", "SNR / WR shell", "Supernova remnant / WR shell"),
        new DiscoveryCategoryDefinition("cluster", "Cluster / star field", "Star cluster or broadband star field")
    };

    public static IReadOnlyList<string> SelectedKeys(NightSageSettings settings) {
        var keys = new List<string>();
        if (settings.DiscoverEmissionNebulae) keys.Add("emission");
        if (settings.DiscoverReflectionDarkNebulae) keys.Add("reflection_dark");
        if (settings.DiscoverGalaxies) keys.Add("galaxy");
        if (settings.DiscoverPlanetaryNebulae) keys.Add("planetary");
        if (settings.DiscoverSnrWrShells) keys.Add("snr_wr");
        if (settings.DiscoverClustersStarFields) keys.Add("cluster");
        return keys;
    }

    public static DiscoveryCategoryDefinition? Find(string key) =>
        All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
}
