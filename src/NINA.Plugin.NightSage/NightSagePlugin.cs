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
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NINA.Plugin.NightSage;

[Export(typeof(IPluginManifest))]
public sealed class NightSagePlugin : PluginBase, INotifyPropertyChanged {
    private readonly NightSageSettingsStore store = NightSageSettingsStore.Instance;
    private NightSageSettings settings;
    private string providerTestStatus = "";

    [ImportingConstructor]
    public NightSagePlugin(
        IProfileService profileService,
        ICameraMediator cameraMediator,
        IFramingAssistantVM framingAssistantVM,
        IApplicationMediator applicationMediator) {
        settings = store.Load();
        Workspace = new NightSageDockable(profileService, cameraMediator, framingAssistantVM, applicationMediator);
        TestProviderCommand = new AsyncRelayCommand(TestProviderAsync);
        OpenProviderHelpCommand = new AsyncRelayCommand(OpenProviderHelpAsync);
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
            Save();
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(ProviderHelpText));
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
    public string ProviderHelpText => Provider switch {
        "OpenAI" => "Get an OpenAI API key ↗",
        "Anthropic" => "Get an Anthropic API key ↗",
        "Google Gemini" => "Get a Gemini API key ↗",
        _ => "OpenAI-compatible setup guide ↗"
    };
    public ICommand TestProviderCommand { get; }
    public ICommand OpenProviderHelpCommand { get; }

    private async Task TestProviderAsync() {
        ProviderTestStatus = "Testing…";
        try {
            var provider = LlmProviderFactory.Create(store.Load());
            var result = await provider.CompleteJsonAsync("Return JSON only.", """Return exactly {"ok":true}.""", CancellationToken.None);
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

    private Task OpenProviderHelpAsync() {
        var url = Provider switch {
            "OpenAI" => "https://help.openai.com/en/articles/4936850-where-do-i-find-my-secret-api-key",
            "Anthropic" => "https://docs.anthropic.com/en/home",
            "Google Gemini" => "https://ai.google.dev/gemini-api/docs/api-key",
            _ => "https://github.com/grm/nina.plugin.nightsage/blob/main/docs/PROVIDERS.md#openai-compatible-endpoints"
        };

        try {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        } catch (Exception ex) {
            Notification.ShowError("NightSage could not open the provider documentation: " + ex.Message);
            Logger.Error("NightSage provider documentation launch failed: " + ex.Message);
        }

        return Task.CompletedTask;
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
