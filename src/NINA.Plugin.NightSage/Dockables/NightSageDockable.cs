using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.NightSage.Infrastructure;
using NINA.Plugin.NightSage.Models;
using AutonomyModeEnum = NINA.Plugin.NightSage.Models.AutonomyMode;
using NINA.Plugin.NightSage.Providers;
using NINA.Plugin.NightSage.Services;
using NINA.Profile.Interfaces;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.WPF.Base.ViewModel;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NINA.Plugin.NightSage.Dockables;

[Export(typeof(IDockableVM))]
public sealed class NightSageDockable : DockableVM {
    private readonly EquipmentContextService equipment;
    private readonly PlanningService planner = new();
    private readonly DiscoveryService discovery = new();
    private readonly TargetSchedulerReflectionAdapter targetScheduler = new();
    private readonly NightSageSettingsStore settingsStore = NightSageSettingsStore.Instance;
    private readonly DispatcherTimer targetSchedulerDetectionTimer;

    private readonly AsyncRelayCommand refreshCommand;
    private readonly AsyncRelayCommand analyzeCommand;
    private readonly AsyncRelayCommand discoverCommand;
    private readonly AsyncRelayCommand planSelectedCommand;
    private readonly AsyncRelayCommand createCommand;

    private bool busy;
    private string targetQuery = "";
    private string userPreferences = "";
    private string setupSummary = "";
    private string targetSchedulerStatus = "";
    private string status = "Ready";
    private ImagingPlan? currentPlan;
    private TargetCandidate? selectedCandidate;
    private AutonomyModeEnum autonomyMode;

    [ImportingConstructor]
    public NightSageDockable(IProfileService profileService, ICameraMediator cameraMediator) : base(profileService) {
        equipment = new EquipmentContextService(profileService, cameraMediator);
        var dict = new ResourceDictionary { Source = new Uri("NINA.Plugin.NightSage;component/Dockables/NightSageDockableTemplates.xaml", UriKind.RelativeOrAbsolute) };
        ImageGeometry = (GeometryGroup)dict["NightSage_Icon"];
        ImageGeometry.Freeze();
        Title = "NightSage";
        autonomyMode = settingsStore.Load().AutonomyMode;

        refreshCommand = new AsyncRelayCommand(() => ExecuteBusyAsync(RefreshAsync), CanRun);
        analyzeCommand = new AsyncRelayCommand(() => ExecuteBusyAsync(AnalyzeAsync), () => CanRun() && !string.IsNullOrWhiteSpace(TargetQuery));
        discoverCommand = new AsyncRelayCommand(() => ExecuteBusyAsync(DiscoverAsync), CanRun);
        planSelectedCommand = new AsyncRelayCommand(() => ExecuteBusyAsync(PlanSelectedAsync), () => CanRun() && SelectedCandidate != null);
        createCommand = new AsyncRelayCommand(() => ExecuteBusyAsync(CreateCurrentPlanAsync), () => CanRun() && CurrentPlan?.IsValidated == true && targetScheduler.CanWrite);
        RefreshCommand = refreshCommand; AnalyzeCommand = analyzeCommand; DiscoverCommand = discoverCommand;
        PlanSelectedCommand = planSelectedCommand; CreateCommand = createCommand;

        profileService.ProfileChanged += ProfileService_ProfileChanged;
        targetSchedulerDetectionTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        targetSchedulerDetectionTimer.Tick += TargetSchedulerDetectionTimer_Tick;
        targetSchedulerDetectionTimer.Start();
        _ = RefreshWithoutBusyAsync();
    }

    public ICommand RefreshCommand { get; }
    public ICommand AnalyzeCommand { get; }
    public ICommand DiscoverCommand { get; }
    public ICommand PlanSelectedCommand { get; }
    public ICommand CreateCommand { get; }
    public IReadOnlyList<AutonomyModeEnum> AutonomyModes { get; } = Enum.GetValues<AutonomyModeEnum>();
    public ObservableCollection<TargetCandidate> Candidates { get; } = new();
    public ObservableCollection<ExposureTemplateChoice> TemplateChoices { get; } = new();

    public string TargetQuery { get => targetQuery; set { targetQuery = value ?? ""; RaisePropertyChanged(); RaiseCommands(); } }
    public string UserPreferences { get => userPreferences; set { userPreferences = value ?? ""; RaisePropertyChanged(); } }
    public string SetupSummary { get => setupSummary; private set { setupSummary = value; RaisePropertyChanged(); } }
    public string TargetSchedulerStatus { get => targetSchedulerStatus; private set { targetSchedulerStatus = value; RaisePropertyChanged(); } }
    public string Status { get => status; private set { status = value; RaisePropertyChanged(); } }
    public bool IsBusy { get => busy; private set { busy = value; RaisePropertyChanged(); RaiseCommands(); } }

    public AutonomyModeEnum AutonomyMode {
        get => autonomyMode;
        set { autonomyMode = value; var s = settingsStore.Load(); s.AutonomyMode = value; settingsStore.Save(s); RaisePropertyChanged(); }
    }

    public ImagingPlan? CurrentPlan {
        get => currentPlan;
        private set { currentPlan = value; RaisePropertyChanged(); RaisePropertyChanged(nameof(PlanSummary)); RaiseCommands(); }
    }

    public string PlanSummary {
        get {
            if (CurrentPlan == null) return "No plan yet.";
            var p = CurrentPlan; var sb = new StringBuilder();
            sb.AppendLine($"{p.TargetName} — {p.TargetType}");
            sb.AppendLine($"RA {p.RaHours:0.0000}h · Dec {p.DecDeg:+0.0000;-0.0000;0}° · rotation {p.RotationDegrees:0}°");
            sb.AppendLine($"Min altitude {p.MinimumAltitudeDegrees:0}° · minimum session {p.MinimumSessionMinutes} min · {p.ProjectPriority} priority");
            sb.AppendLine(); sb.AppendLine(p.StrategySummary);
            if (p.Warnings.Count > 0) { sb.AppendLine(); foreach (var warning in p.Warnings) sb.AppendLine("⚠ " + warning); }
            return sb.ToString().Trim();
        }
    }

    public TargetCandidate? SelectedCandidate { get => selectedCandidate; set { selectedCandidate = value; RaisePropertyChanged(); RaiseCommands(); } }
    private bool CanRun() => !IsBusy;

    private async Task ExecuteBusyAsync(Func<Task> action) {
        if (IsBusy) return;
        IsBusy = true;
        try { await action(); }
        catch (Exception ex) { Status = "Error: " + ex.Message; Logger.Error("NightSage: " + ex); Notification.ShowError("NightSage: " + ex.Message); }
        finally { IsBusy = false; }
    }

    private Task RefreshAsync() => RefreshWithoutBusyAsync();

    private Task RefreshWithoutBusyAsync() {
        try {
            var setup = equipment.Capture();
            SetupSummary = setup.Summary;
            TargetSchedulerStatus = targetScheduler.Status;
            if (CurrentPlan != null) RebuildTemplateChoices(setup);
            Status = "Ready";
        } catch (Exception ex) {
            SetupSummary = "Unable to read active profile: " + ex.Message;
            TargetSchedulerStatus = targetScheduler.Status;
        }
        return Task.CompletedTask;
    }

    private async Task AnalyzeAsync() {
        Status = $"Resolving and planning {TargetQuery.Trim()}…";
        var setup = equipment.Capture();
        SetupSummary = setup.Summary;
        TargetSchedulerStatus = targetScheduler.Status;
        var templates = targetScheduler.GetExposureTemplates(setup.ProfileId);
        var provider = LlmProviderFactory.Create(settingsStore.Load());
        CurrentPlan = await planner.BuildPlanAsync(TargetQuery.Trim(), UserPreferences, setup, templates, provider, CancellationToken.None);
        RebuildTemplateChoices(setup);
        Status = $"Plan ready: {CurrentPlan.TargetName}";
        if (AutonomyMode is AutonomyModeEnum.Create or AutonomyModeEnum.Autopilot) await CreateCurrentPlanCoreAsync();
    }

    private async Task DiscoverAsync() {
        Status = "Finding targets for the next 7 days…";
        var setup = equipment.Capture(); SetupSummary = setup.Summary;
        var settings = settingsStore.Load();
        var existing = settings.IncludeExistingTargets ? targetScheduler.GetExistingTargets(setup.ProfileId) : Array.Empty<ExistingTargetInfo>();
        var provider = LlmProviderFactory.Create(settings);
        var result = await discovery.DiscoverAsync(setup, existing, provider, Math.Clamp(settings.DiscoveryDays, 1, 14), settings.MinimumAltitudeDegrees, CancellationToken.None);
        Candidates.Clear(); foreach (var c in result.Candidates) Candidates.Add(c);
        SelectedCandidate = Candidates.FirstOrDefault();
        Status = Candidates.Count == 0 ? "No candidate survived deterministic visibility/resolution checks." : $"{Candidates.Count} candidate(s) ready.";
        if (AutonomyMode == AutonomyModeEnum.Autopilot && SelectedCandidate != null) { await PlanSelectedCoreAsync(); if (CurrentPlan != null) await CreateCurrentPlanCoreAsync(); }
    }

    private Task PlanSelectedAsync() => PlanSelectedCoreAsync();

    private async Task PlanSelectedCoreAsync() {
        if (SelectedCandidate == null) return;
        TargetQuery = SelectedCandidate.Name;
        Status = $"Building full plan for {SelectedCandidate.Name}…";
        var setup = equipment.Capture();
        var provider = LlmProviderFactory.Create(settingsStore.Load());
        var templates = targetScheduler.GetExposureTemplates(setup.ProfileId);
        var discoveryNote = $"Discovered as {SelectedCandidate.Category}. {SelectedCandidate.Reason}. " + UserPreferences;
        CurrentPlan = await planner.BuildPlanAsync(SelectedCandidate.Name, discoveryNote, setup, templates, provider, CancellationToken.None);
        RebuildTemplateChoices(setup);
        Status = $"Plan ready: {CurrentPlan.TargetName}";
        if (AutonomyMode == AutonomyModeEnum.Create) await CreateCurrentPlanCoreAsync();
    }

    private Task CreateCurrentPlanAsync() => CreateCurrentPlanCoreAsync();

    private async Task CreateCurrentPlanCoreAsync() {
        if (CurrentPlan == null) return;
        var setup = equipment.Capture();
        TargetSchedulerStatus = targetScheduler.Status;
        if (!targetScheduler.CanWrite) throw new InvalidOperationException(targetScheduler.Status);
        if (TemplateChoices.Count != CurrentPlan.Exposures.Count) RebuildTemplateChoices(setup);

        var pending = TemplateChoices.Where(x => x.RequiresCreation).ToList();
        if (pending.Count > 0) {
            var lines = string.Join(Environment.NewLine, pending.Select(x => "• " + x.ActionSummary));
            var answer = MessageBox.Show(
                "This plan needs new Target Scheduler exposure template(s):" + Environment.NewLine + Environment.NewLine + lines +
                Environment.NewLine + Environment.NewLine +
                "Derived templates keep the selected base template's gain, offset, binning, readout mode, moon avoidance, twilight, dithering and humidity settings. Only the exposure duration is changed." +
                Environment.NewLine + Environment.NewLine + "Create these templates and the project?",
                "NightSage — confirm template creation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) { Status = "Creation cancelled — choose another template mapping or edit the plan."; return; }
        }

        Status = "Creating project in Target Scheduler…";
        var result = await targetScheduler.CreateAsync(setup.ProfileId, CurrentPlan, TemplateChoices.ToList(), settingsStore.Load().ActivateCreatedProjects, CancellationToken.None);
        Status = result.Message;
        if (result.Success) { Notification.ShowSuccess("NightSage: " + result.Message); TargetSchedulerStatus = targetScheduler.Status; }
        else Notification.ShowWarning("NightSage: " + result.Message);
    }

    private void RebuildTemplateChoices(SetupContext setup) {
        TemplateChoices.Clear();
        if (CurrentPlan == null) return;
        var templates = targetScheduler.GetExposureTemplates(setup.ProfileId);
        foreach (var choice in TemplateChoiceService.Build(CurrentPlan, templates)) TemplateChoices.Add(choice);
    }

    private void TargetSchedulerDetectionTimer_Tick(object? sender, EventArgs e) {
        var newStatus = targetScheduler.Status;
        if (!string.Equals(TargetSchedulerStatus, newStatus, StringComparison.Ordinal)) {
            TargetSchedulerStatus = newStatus;
            try { if (CurrentPlan != null) RebuildTemplateChoices(equipment.Capture()); } catch { }
            RaiseCommands();
        }
        if (targetScheduler.CanWrite) targetSchedulerDetectionTimer.Stop();
    }

    private void ProfileService_ProfileChanged(object? sender, EventArgs e) {
        targetSchedulerDetectionTimer.Start();
        _ = RefreshWithoutBusyAsync();
    }

    private void RaiseCommands() {
        refreshCommand?.RaiseCanExecuteChanged(); analyzeCommand?.RaiseCanExecuteChanged(); discoverCommand?.RaiseCanExecuteChanged();
        planSelectedCommand?.RaiseCanExecuteChanged(); createCommand?.RaiseCanExecuteChanged();
    }
}
