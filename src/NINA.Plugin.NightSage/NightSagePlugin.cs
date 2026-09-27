using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Plugin.NightSage.Dockables;
using NINA.Plugin.NightSage.Infrastructure;
using NINA.Plugin.NightSage.Models;
using NINA.Plugin.NightSage.Providers;
using NINA.Profile.Interfaces;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NINA.Plugin.NightSage;

[Export(typeof(IPluginManifest))]
public sealed class NightSagePlugin : PluginBase, INotifyPropertyChanged {
    private readonly NightSageSettingsStore store = NightSageSettingsStore.Instance;
    private NightSageSettings settings;
    private string providerTestStatus = "";

    [ImportingConstructor]
    public NightSagePlugin(IProfileService profileService, ICameraMediator cameraMediator) {
        settings = store.Load();
        Workspace = new NightSageDockable(profileService, cameraMediator);
        TestProviderCommand = new AsyncRelayCommand(TestProviderAsync);
        Logger.Info("NightSage: plugin initialized");
    }

    public NightSageDockable Workspace { get; }
    public IReadOnlyList<string> Providers { get; } = new[] { "OpenAI", "Anthropic", "Google Gemini", "OpenAI-compatible" };
    public IReadOnlyList<AutonomyMode> AutonomyModes { get; } = Enum.GetValues<AutonomyMode>();

    public string Provider {
        get => settings.Provider;
        set {
            if (settings.Provider == value) return;
            settings.Provider = value;
            if (string.IsNullOrWhiteSpace(settings.Model) || IsKnownDefault(settings.Model)) {
                settings.Model = value switch {
                    "OpenAI" => "gpt-5.6-terra",
                    "Anthropic" => "claude-sonnet-4-5",
                    "Google Gemini" => "gemini-2.5-pro",
                    _ => ""
                };
                RaisePropertyChanged(nameof(Model));
            }
            Save(); RaisePropertyChanged();
        }
    }

    public string Model { get => settings.Model; set { settings.Model = value ?? ""; Save(); RaisePropertyChanged(); } }
    public string Endpoint { get => settings.Endpoint; set { settings.Endpoint = value ?? ""; Save(); RaisePropertyChanged(); } }
    public string ApiKey {
        get => NightSageSettingsStore.UnprotectSecret(settings.ApiKeyProtected);
        set { settings.ApiKeyProtected = NightSageSettingsStore.ProtectSecret(value); Save(); RaisePropertyChanged(); }
    }
    public AutonomyMode AutonomyMode { get => settings.AutonomyMode; set { settings.AutonomyMode = value; Save(); RaisePropertyChanged(); } }
    public bool ActivateCreatedProjects { get => settings.ActivateCreatedProjects; set { settings.ActivateCreatedProjects = value; Save(); RaisePropertyChanged(); } }
    public bool IncludeExistingTargets { get => settings.IncludeExistingTargets; set { settings.IncludeExistingTargets = value; Save(); RaisePropertyChanged(); } }
    public double MinimumAltitudeDegrees { get => settings.MinimumAltitudeDegrees; set { settings.MinimumAltitudeDegrees = Math.Clamp(value, 15, 80); Save(); RaisePropertyChanged(); } }
    public string ProviderTestStatus { get => providerTestStatus; private set { providerTestStatus = value; RaisePropertyChanged(); } }
    public ICommand TestProviderCommand { get; }

    private async Task TestProviderAsync() {
        ProviderTestStatus = "Testing…";
        try {
            var provider = LlmProviderFactory.Create(store.Load());
            var result = await provider.CompleteJsonAsync("Return JSON only.", "Return exactly {"ok":true}.", CancellationToken.None);
            using var doc = JsonPayload.ParseObject(result);
            var ok = doc.RootElement.TryGetProperty("ok", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.True;
            ProviderTestStatus = ok ? "✓ Provider connected" : "⚠ Connected, unexpected response";
            if (ok) Notification.ShowSuccess("NightSage LLM provider connected.");
        } catch (Exception ex) {
            ProviderTestStatus = "✗ " + ex.Message;
            Notification.ShowError("NightSage provider test failed: " + ex.Message);
            Logger.Error("NightSage provider test failed: " + ex.Message);
        }
    }

    private void Save() => store.Save(settings);
    private static bool IsKnownDefault(string model) => model is "gpt-5.6-terra" or "claude-sonnet-4-5" or "gemini-2.5-pro";

    public override Task Teardown() {
        Workspace.Dispose();
        Logger.Info("NightSage: plugin teardown");
        return base.Teardown();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
