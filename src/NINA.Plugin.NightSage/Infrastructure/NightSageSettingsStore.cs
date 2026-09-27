using System.IO;
using NINA.Plugin.NightSage.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NINA.Plugin.NightSage.Infrastructure;

public sealed class NightSageSettings {
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = "gpt-5.6-terra";
    public string Endpoint { get; set; } = "";
    public string ApiKeyProtected { get; set; } = "";
    public AutonomyMode AutonomyMode { get; set; } = AutonomyMode.Preview;
    public IntegrationAmbition IntegrationAmbition { get; set; } = IntegrationAmbition.Balanced;
    public bool ActivateCreatedProjects { get; set; } = false;
    public double MinimumAltitudeDegrees { get; set; } = 30;
    public int DiscoveryDays { get; set; } = 7;
    public int MaxDiscoveryCandidates { get; set; } = 5;
    public bool IncludeExistingTargets { get; set; } = true;
}

public sealed class NightSageSettingsStore {
    private static readonly Lazy<NightSageSettingsStore> Lazy = new(() => new NightSageSettingsStore());
    public static NightSageSettingsStore Instance => Lazy.Value;

    private readonly object gate = new();
    private readonly string path;
    private NightSageSettings? cached;

    private NightSageSettingsStore() {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NINA", "NightSage");
        Directory.CreateDirectory(dir);
        path = Path.Combine(dir, "settings.json");
    }

    public NightSageSettings Load() {
        lock (gate) {
            if (cached != null) return Clone(cached);
            if (!File.Exists(path)) {
                cached = new NightSageSettings();
                return Clone(cached);
            }

            try {
                cached = JsonSerializer.Deserialize<NightSageSettings>(File.ReadAllText(path)) ?? new NightSageSettings();
            } catch {
                cached = new NightSageSettings();
            }

            return Clone(cached);
        }
    }

    public void Save(NightSageSettings settings) {
        lock (gate) {
            cached = Clone(settings);
            var json = JsonSerializer.Serialize(cached, new JsonSerializerOptions { WriteIndented = true });
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json, Encoding.UTF8);
            File.Move(tmp, path, true);
        }
    }

    public static string ProtectSecret(string? value) {
        if (string.IsNullOrEmpty(value)) return "";
        var raw = Encoding.UTF8.GetBytes(value);
        var encrypted = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public static string UnprotectSecret(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return "";
        try {
            var encrypted = Convert.FromBase64String(value);
            var raw = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(raw);
        } catch {
            return "";
        }
    }

    private static NightSageSettings Clone(NightSageSettings source) =>
        JsonSerializer.Deserialize<NightSageSettings>(JsonSerializer.Serialize(source)) ?? new NightSageSettings();
}
