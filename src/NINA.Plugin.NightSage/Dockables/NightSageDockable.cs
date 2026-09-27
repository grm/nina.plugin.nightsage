using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.NightSage.Infrastructure;
using NINA.Plugin.NightSage.Models;
using AutonomyModeEnum = NINA.Plugin.NightSage.Models.AutonomyMode;
using IntegrationAmbitionEnum = NINA.Plugin.NightSage.Models.IntegrationAmbition;
using NINA.Plugin.NightSage.Providers;
using NINA.Plugin.NightSage.Services;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.ViewModel;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NINA.Plugin.NightSage.Dockables;

public sealed class NightSageDockable : DockableVM, IDisposable {
    private readonly EquipmentContextService equipment;
    private readonly IProfileService profileService;
    private readonly PlanningService planner = new();
    private readonly DiscoveryService discovery = new();
    private readonly TargetSchedulerReflectionAdapter targetScheduler = new();
    private readonly NightSageSettingsStore settingsStore = NightSageSettingsStore.Instance;
    private readonly DispatcherTimer targetSchedulerDetectionTimer;
    private readonly FramingAssistantIntegration framingIntegration;

    private readonly AsyncRelayCommand refreshCommand;
    private readonly AsyncRelayCommand analyzeCommand;
    private readonly AsyncRelayCommand discoverCommand;
    private readonly AsyncRelayCommand planSelectedCommand;
    private readonly AsyncRelayCommand createCommand;
    private readonly AsyncRelayCommand loadCurrentPlanIntoFramingCommand;
    private readonly AsyncRelayCommand recalculateFromFramingCommand;
    private readonly AsyncRelayCommand openNativeFramingCommand;

    private bool busy;
    private bool disposed;
    private string targetQuery = "";
    private string userPreferences = "";
    private string setupSummary = "";
    private string targetSchedulerStatus = "";
    private string status = "Ready";
    private ImagingPlan? currentPlan;
    private TargetCandidate? selectedCandidate;
    private AutonomyModeEnum autonomyMode;
    private IntegrationAmbitionEnum integrationAmbition;
    private FramingSnapshot? currentFraming;
    private bool framingAssociatedWithCurrentPlan;
    private bool framingPlanOutdated;
    private string framingBaselineFingerprint = "";
    private int selectedWorkspaceTabIndex;

    public NightSageDockable(
        IProfileService profileService,
        ICameraMediator cameraMediator,
        IFramingAssistantVM framingAssistantVM,
        IApplicationMediator applicationMediator) : base(profileService) {

        this.profileService = profileService;
        equipment = new EquipmentContextService(profileService, cameraMediator);
        framingIntegration = new FramingAssistantIntegration(framingAssistantVM, profileService, applicationMediator);
        framingIntegration.FramingChanged += FramingIntegration_FramingChanged;

        var dict = new ResourceDictionary { Source = new Uri("NINA.Plugin.NightSage;component/Dockables/NightSageDockableTemplates.xaml", UriKind.RelativeOrAbsolute) };
        ImageGeometry = (GeometryGroup)dict["NightSage_Icon"];
        ImageGeometry.Freeze();
        Title = "NightSage";

        var initialSettings = settingsStore.Load();
        autonomyMode = initialSettings.AutonomyMode;
        integrationAmbition = initialSettings.IntegrationAmbition;

        refreshCommand = new AsyncRelayCommand(() => ExecuteBusyAsync(RefreshAsync), CanRun);
        analyzeCommand = new AsyncRelayCommand(() => ExecuteBusyAsync(AnalyzeAsync), () => CanRun() && !string.IsNullOrWhiteSpace(TargetQuery));
        discoverCommand = new AsyncRelayCommand(() => ExecuteBusyAsync(DiscoverAsync), CanRun);
        planSelectedCommand = new AsyncRelayCommand(() => ExecuteBusyAsync(PlanSelectedAsync), () => CanRun() && SelectedCandidate != null);
        createCommand = new AsyncRelayCommand(() => ExecuteBusyAsync(CreateCurrentPlanAsync), () => CanRun() && CurrentPlan?.IsValidated == true && targetScheduler.CanWrite && (!framingAssociatedWithCurrentPlan || !FramingPlanOutdated));
        loadCurrentPlanIntoFramingCommand = new AsyncRelayCommand(
            () => ExecuteBusyAsync(LoadCurrentPlanIntoFramingAsync),
            () => CanRun() && CurrentPlan?.IsValidated == true && framingIntegration.EmbeddedAvailable);
        recalculateFromFramingCommand = new AsyncRelayCommand(
            () => ExecuteBusyAsync(RecalculateFromFramingAsync),
            () => CanRun() && CurrentPlan?.IsValidated == true && framingAssociatedWithCurrentPlan && CurrentFraming?.Panels.Count > 0);
        openNativeFramingCommand = new AsyncRelayCommand(OpenNativeFramingAsync, CanRun);

        RefreshCommand = refreshCommand;
        AnalyzeCommand = analyzeCommand;
        DiscoverCommand = discoverCommand;
        PlanSelectedCommand = planSelectedCommand;
        CreateCommand = createCommand;
        LoadCurrentPlanIntoFramingCommand = loadCurrentPlanIntoFramingCommand;
        RecalculateFromFramingCommand = recalculateFromFramingCommand;
        OpenNativeFramingCommand = openNativeFramingCommand;

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
    public ICommand LoadCurrentPlanIntoFramingCommand { get; }
    public ICommand RecalculateFromFramingCommand { get; }
    public ICommand OpenNativeFramingCommand { get; }

    public IReadOnlyList<AutonomyModeEnum> AutonomyModes { get; } = Enum.GetValues<AutonomyModeEnum>();
    public IReadOnlyList<IntegrationAmbitionEnum> IntegrationAmbitions { get; } = Enum.GetValues<IntegrationAmbitionEnum>();
    public ObservableCollection<TargetCandidate> Candidates { get; } = new();
    public ObservableCollection<ExposureTemplateChoice> TemplateChoices { get; } = new();

    public IFramingAssistantVM NativeFramingViewModel => framingIntegration.ViewModel;
    public DataTemplate? NativeFramingTemplate => framingIntegration.EmbeddedTemplate;
    public string FramingEmbedStatus => framingIntegration.EmbedStatus;

    public int SelectedWorkspaceTabIndex {
        get => selectedWorkspaceTabIndex;
        set {
            if (selectedWorkspaceTabIndex == value) return;
            selectedWorkspaceTabIndex = Math.Clamp(value, 0, 2);
            RaisePropertyChanged();
        }
    }

    public string TargetQuery { get => targetQuery; set { targetQuery = value ?? ""; RaisePropertyChanged(); RaiseCommands(); } }
    public string UserPreferences { get => userPreferences; set { userPreferences = value ?? ""; RaisePropertyChanged(); } }
    public string SetupSummary { get => setupSummary; private set { setupSummary = value; RaisePropertyChanged(); } }
    public string TargetSchedulerStatus { get => targetSchedulerStatus; private set { targetSchedulerStatus = value; RaisePropertyChanged(); } }
    public string Status { get => status; private set { status = value; RaisePropertyChanged(); } }
    public bool IsBusy { get => busy; private set { busy = value; RaisePropertyChanged(); RaiseCommands(); } }

    public AutonomyModeEnum AutonomyMode {
        get => autonomyMode;
        set {
            if (autonomyMode == value) return;
            autonomyMode = value;
            var s = settingsStore.Load();
            s.AutonomyMode = value;
            settingsStore.Save(s);
            RaisePropertyChanged();
        }
    }

    public IntegrationAmbitionEnum IntegrationAmbition {
        get => integrationAmbition;
        set {
            if (integrationAmbition == value) return;
            integrationAmbition = value;
            var s = settingsStore.Load();
            s.IntegrationAmbition = value;
            settingsStore.Save(s);
            ClearPlanAndDiscovery();
            Status = $"Integration ambition: {value}. Run discovery or analyze a target.";
            RaisePropertyChanged();
            RaiseCommands();
        }
    }

    public int DiscoveryResultLimit {
        get => settingsStore.Load().DiscoveryResultLimit;
        set {
            var s = settingsStore.Load();
            var normalized = Math.Clamp(value, 1, 30);
            if (s.DiscoveryResultLimit == normalized) return;
            s.DiscoveryResultLimit = normalized;
            settingsStore.Save(s);
            ClearPlanAndDiscovery();
            RaisePropertyChanged();
        }
    }

    public bool DiscoverEmissionNebulae {
        get => settingsStore.Load().DiscoverEmissionNebulae;
        set => UpdateDiscoveryOption(nameof(DiscoverEmissionNebulae), s => s.DiscoverEmissionNebulae = value);
    }
    public bool DiscoverReflectionDarkNebulae {
        get => settingsStore.Load().DiscoverReflectionDarkNebulae;
        set => UpdateDiscoveryOption(nameof(DiscoverReflectionDarkNebulae), s => s.DiscoverReflectionDarkNebulae = value);
    }
    public bool DiscoverGalaxies {
        get => settingsStore.Load().DiscoverGalaxies;
        set => UpdateDiscoveryOption(nameof(DiscoverGalaxies), s => s.DiscoverGalaxies = value);
    }
    public bool DiscoverPlanetaryNebulae {
        get => settingsStore.Load().DiscoverPlanetaryNebulae;
        set => UpdateDiscoveryOption(nameof(DiscoverPlanetaryNebulae), s => s.DiscoverPlanetaryNebulae = value);
    }
    public bool DiscoverSnrWrShells {
        get => settingsStore.Load().DiscoverSnrWrShells;
        set => UpdateDiscoveryOption(nameof(DiscoverSnrWrShells), s => s.DiscoverSnrWrShells = value);
    }
    public bool DiscoverClustersStarFields {
        get => settingsStore.Load().DiscoverClustersStarFields;
        set => UpdateDiscoveryOption(nameof(DiscoverClustersStarFields), s => s.DiscoverClustersStarFields = value);
    }

    public ImagingPlan? CurrentPlan {
        get => currentPlan;
        private set {
            currentPlan = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(PlanSummary));
            RaisePropertyChanged(nameof(FramingNotice));
            RaiseCommands();
        }
    }

    public FramingSnapshot? CurrentFraming {
        get => currentFraming;
        private set {
            currentFraming = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(FramingSummary));
            RaisePropertyChanged(nameof(FramingNotice));
            RaiseCommands();
        }
    }

    public bool FramingPlanOutdated {
        get => framingPlanOutdated;
        private set {
            if (framingPlanOutdated == value) return;
            framingPlanOutdated = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(FramingNotice));
        }
    }

    public string FramingSummary => CurrentFraming?.Summary ?? "No framing associated with the current plan.";

    public string FramingNotice {
        get {
            if (CurrentPlan == null) return "Build or analyze a target first, then load it into Framing.";
            if (!framingAssociatedWithCurrentPlan) return "Current plan is not loaded into Framing yet.";
            if (FramingPlanOutdated) return "Framing changed — recalculate the acquisition plan before creating it in Target Scheduler.";
            return "Framing synchronized with the acquisition plan.";
        }
    }

    public string PlanSummary {
        get {
            if (CurrentPlan == null) return "";
            var p = CurrentPlan;
            var sb = new StringBuilder();
            sb.AppendLine($"{p.TargetName} — {p.TargetType}");
            sb.AppendLine($"RA {p.RaHours:0.0000}h · Dec {p.DecDeg:+0.0000;-0.0000;0}° · rotation {p.RotationDegrees:0}°");
            sb.AppendLine($"Integration ambition {p.Ambition}");
            if (p.IsMosaic) {
                sb.AppendLine($"Framing {p.Framing?.GridDisplay} · {p.PanelCount} panels · {p.Framing?.OverlapDisplay} overlap");
                sb.AppendLine($"Integration per panel {p.IntegrationPerPanelDisplay} · project total {p.TotalIntegrationDisplay}");
            } else {
                sb.AppendLine($"Planned integration {p.TotalIntegrationDisplay}");
            }
            sb.AppendLine($"Min altitude {p.MinimumAltitudeDegrees:0}° · minimum session {p.MinimumSessionMinutes} min · {p.ProjectPriority} priority");
            sb.AppendLine();
            sb.AppendLine(p.StrategySummary);
            if (p.Warnings.Count > 0) {
                sb.AppendLine();
                foreach (var warning in p.Warnings) sb.AppendLine("⚠ " + warning);
            }
            return sb.ToString().Trim();
        }
    }

    public TargetCandidate? SelectedCandidate {
        get => selectedCandidate;
        set {
            selectedCandidate = value;
            RaisePropertyChanged();
            RaiseCommands();
        }
    }

    private bool CanRun() => !IsBusy;

    private async Task ExecuteBusyAsync(Func<Task> action) {
        if (IsBusy) return;
        IsBusy = true;
        try {
            await action();
        } catch (Exception ex) {
            Status = "Error: " + ex.Message;
            Logger.Error("NightSage: " + ex);
            Notification.ShowError("NightSage: " + ex.Message);
        } finally {
            IsBusy = false;
        }
    }

    private void ResetFramingAssociation() {
        currentFraming = null;
        framingAssociatedWithCurrentPlan = false;
        framingBaselineFingerprint = "";
        framingPlanOutdated = false;
        RaisePropertyChanged(nameof(CurrentFraming));
        RaisePropertyChanged(nameof(FramingSummary));
        RaisePropertyChanged(nameof(FramingPlanOutdated));
        RaisePropertyChanged(nameof(FramingNotice));
        RaiseCommands();
    }

    private void ClearPlan() {
        CurrentPlan = null;
        TemplateChoices.Clear();
        ResetFramingAssociation();
    }

    private void ClearPlanAndDiscovery() {
        ClearPlan();
        Candidates.Clear();
        SelectedCandidate = null;
    }

    private void UpdateDiscoveryOption(string propertyName, Action<NightSageSettings> update) {
        var s = settingsStore.Load();
        update(s);
        settingsStore.Save(s);
        ClearPlanAndDiscovery();
        RaisePropertyChanged(propertyName);
        RaiseCommands();
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
        framingIntegration.Reset();
        ClearPlan();
        Status = $"Resolving and planning {TargetQuery.Trim()}…";
        var setup = equipment.Capture();
        SetupSummary = setup.Summary;
        TargetSchedulerStatus = targetScheduler.Status;
        var templates = targetScheduler.GetExposureTemplates(setup.ProfileId);
        var provider = LlmProviderFactory.Create(settingsStore.Load());
        CurrentPlan = await planner.BuildPlanAsync(TargetQuery.Trim(), UserPreferences, setup, templates, IntegrationAmbition, provider, CancellationToken.None);
        ResetFramingAssociation();
        RebuildTemplateChoices(setup);
        Status = $"Plan ready: {CurrentPlan.TargetName} · {CurrentPlan.TotalIntegrationDisplay}. Open Framing to choose the composition.";
        if (AutonomyMode is AutonomyModeEnum.Create or AutonomyModeEnum.Autopilot)
            await CreateCurrentPlanCoreAsync();
    }

    private async Task DiscoverAsync() {
        framingIntegration.Reset();
        ClearPlanAndDiscovery();
        var settings = settingsStore.Load();
        var categories = DiscoveryCategoryCatalog.SelectedKeys(settings);
        if (categories.Count == 0) throw new InvalidOperationException("Select at least one target type before running Find targets.");

        Status = $"Finding {IntegrationAmbition} targets for the next {Math.Clamp(settings.DiscoveryDays, 1, 14)} days…";
        var setup = equipment.Capture();
        SetupSummary = setup.Summary;
        var existing = settings.IncludeExistingTargets
            ? targetScheduler.GetExistingTargets(setup.ProfileId)
            : Array.Empty<ExistingTargetInfo>();
        var provider = LlmProviderFactory.Create(settings);
        var result = await discovery.DiscoverAsync(
            setup, existing, provider,
            Math.Clamp(settings.DiscoveryDays, 1, 14),
            settings.MinimumAltitudeDegrees,
            IntegrationAmbition,
            categories,
            Math.Clamp(settings.DiscoveryResultLimit, 1, 30),
            CancellationToken.None);

        foreach (var c in result.Candidates) Candidates.Add(c);
        SelectedCandidate = Candidates.FirstOrDefault();
        Status = Candidates.Count == 0
            ? "No candidate survived deterministic visibility/resolution checks."
            : $"{Candidates.Count} candidate(s) ready for {IntegrationAmbition}.";
        if (AutonomyMode == AutonomyModeEnum.Autopilot && SelectedCandidate != null) {
            await PlanSelectedCoreAsync();
            if (CurrentPlan != null) await CreateCurrentPlanCoreAsync();
        }
    }

    private Task PlanSelectedAsync() => PlanSelectedCoreAsync();

    private async Task PlanSelectedCoreAsync() {
        if (SelectedCandidate == null) return;
        TargetQuery = SelectedCandidate.Name;
        framingIntegration.Reset();
        ClearPlan();
        Status = $"Building full {IntegrationAmbition} plan for {SelectedCandidate.Name}…";
        var setup = equipment.Capture();
        var provider = LlmProviderFactory.Create(settingsStore.Load());
        var templates = targetScheduler.GetExposureTemplates(setup.ProfileId);
        var discoveryNote = $"Discovered as {SelectedCandidate.Category}. {SelectedCandidate.Reason}. " + UserPreferences;
        CurrentPlan = await planner.BuildPlanAsync(SelectedCandidate.Name, discoveryNote, setup, templates, IntegrationAmbition, provider, CancellationToken.None);
        ResetFramingAssociation();
        RebuildTemplateChoices(setup);
        Status = $"Plan ready: {CurrentPlan.TargetName} · {CurrentPlan.TotalIntegrationDisplay}. Open Framing to choose the composition.";
        if (AutonomyMode == AutonomyModeEnum.Create) await CreateCurrentPlanCoreAsync();
    }

    private async Task LoadCurrentPlanIntoFramingAsync() {
        if (CurrentPlan == null) return;

        // Switch first so the embedded native Framing Assistant is measured.
        // N.I.N.A.'s SetCoordinates waits for a non-zero BoundWidth before loading the survey image.
        SelectedWorkspaceTabIndex = 1;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null)
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

        Status = $"Loading {CurrentPlan.TargetName} into the N.I.N.A. Framing Assistant…";
        var ok = await framingIntegration.LoadTargetAsync(
            CurrentPlan.TargetName,
            CurrentPlan.RaHours,
            CurrentPlan.DecDeg,
            CurrentPlan.RotationDegrees,
            resetMosaic: true,
            CancellationToken.None);

        if (!ok) throw new InvalidOperationException("N.I.N.A. could not load the target image in Framing Assistant.");

        var snapshot = framingIntegration.Capture();
        if (snapshot.Panels.Count == 0)
            throw new InvalidOperationException("Framing Assistant did not produce a camera frame.");

        ApplyFramingBaseline(snapshot, updateCurrentPlan: true);
        Status = $"Framing ready: {snapshot.Summary}";
    }

    private async Task RecalculateFromFramingAsync() {
        if (CurrentPlan == null) return;

        var snapshot = framingIntegration.Capture();
        if (snapshot.Panels.Count == 0)
            throw new InvalidOperationException("No framing panels are currently available.");

        var targetName = CurrentPlan.TargetName;
        Status = snapshot.IsMosaic
            ? $"Recalculating acquisition plan for {snapshot.PanelCount} framing panels…"
            : "Recalculating acquisition plan from the approved framing…";

        var setup = equipment.Capture();
        var templates = targetScheduler.GetExposureTemplates(setup.ProfileId);
        var provider = LlmProviderFactory.Create(settingsStore.Load());

        CurrentPlan = await planner.BuildPlanAsync(
            targetName,
            UserPreferences,
            setup,
            templates,
            IntegrationAmbition,
            snapshot,
            provider,
            CancellationToken.None);

        RebuildTemplateChoices(setup);
        ApplyFramingBaseline(snapshot, updateCurrentPlan: true);
        Status = $"Plan recalculated from framing · {CurrentPlan.TotalIntegrationDisplay} total.";
        SelectedWorkspaceTabIndex = 0;
    }

    private Task OpenNativeFramingAsync() {
        framingIntegration.OpenNativeFramingAssistant();
        return Task.CompletedTask;
    }

    private void ApplyFramingBaseline(FramingSnapshot snapshot, bool updateCurrentPlan) {
        CurrentFraming = snapshot;
        framingAssociatedWithCurrentPlan = true;
        framingBaselineFingerprint = snapshot.Fingerprint;
        FramingPlanOutdated = false;

        if (updateCurrentPlan && CurrentPlan != null) {
            CurrentPlan.Framing = snapshot;
            CurrentPlan.RaHours = snapshot.CenterRaHours;
            CurrentPlan.DecDeg = snapshot.CenterDecDeg;
            CurrentPlan.RotationDegrees = snapshot.PrimaryRotationDegrees;
            RaisePropertyChanged(nameof(CurrentPlan));
            RaisePropertyChanged(nameof(PlanSummary));
        }

        RaisePropertyChanged(nameof(FramingNotice));
        RaiseCommands();
    }

    private Task CreateCurrentPlanAsync() => CreateCurrentPlanCoreAsync();

    private async Task CreateCurrentPlanCoreAsync() {
        if (CurrentPlan == null) return;
        if (framingAssociatedWithCurrentPlan && FramingPlanOutdated)
            throw new InvalidOperationException("Framing changed after this plan was calculated. Recalculate the acquisition plan before creating it in Target Scheduler.");

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
                "NightSage — confirm template creation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes) {
                Status = "Creation cancelled — choose another template mapping or edit the plan.";
                return;
            }
        }

        Status = CurrentPlan.IsMosaic
            ? $"Creating {CurrentPlan.PanelCount}-panel mosaic project in Target Scheduler…"
            : "Creating project in Target Scheduler…";

        var result = await targetScheduler.CreateAsync(
            setup.ProfileId,
            CurrentPlan,
            TemplateChoices.ToList(),
            settingsStore.Load().ActivateCreatedProjects,
            CancellationToken.None);

        Status = result.Message;
        if (result.Success) {
            Notification.ShowSuccess("NightSage: " + result.Message);
            TargetSchedulerStatus = targetScheduler.Status;
        } else {
            Notification.ShowWarning("NightSage: " + result.Message);
        }
    }

    private void RebuildTemplateChoices(SetupContext setup) {
        TemplateChoices.Clear();
        if (CurrentPlan == null) return;
        var templates = targetScheduler.GetExposureTemplates(setup.ProfileId);
        foreach (var choice in TemplateChoiceService.Build(CurrentPlan, templates))
            TemplateChoices.Add(choice);
    }

    private void FramingIntegration_FramingChanged(object? sender, EventArgs e) {
        if (!framingAssociatedWithCurrentPlan || CurrentPlan == null) return;

        void update() {
            try {
                var snapshot = framingIntegration.Capture();
                CurrentFraming = snapshot;
                FramingPlanOutdated = snapshot.Fingerprint != framingBaselineFingerprint;
                RaisePropertyChanged(nameof(FramingNotice));
                RaiseCommands();
            } catch (Exception ex) {
                Logger.Warning($"NightSage: failed to read changed framing: {ex.Message}");
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess()) dispatcher.BeginInvoke(update);
        else update();
    }

    private void TargetSchedulerDetectionTimer_Tick(object? sender, EventArgs e) {
        var newStatus = targetScheduler.Status;
        if (!string.Equals(TargetSchedulerStatus, newStatus, StringComparison.Ordinal)) {
            TargetSchedulerStatus = newStatus;
            try {
                if (CurrentPlan != null) RebuildTemplateChoices(equipment.Capture());
            } catch { }
            RaiseCommands();
        }
        if (targetScheduler.CanWrite) targetSchedulerDetectionTimer.Stop();
    }

    private void ProfileService_ProfileChanged(object? sender, EventArgs e) {
        targetSchedulerDetectionTimer.Start();
        framingIntegration.Reset();
        ClearPlanAndDiscovery();
        _ = RefreshWithoutBusyAsync();
    }

    private void RaiseCommands() {
        refreshCommand?.RaiseCanExecuteChanged();
        analyzeCommand?.RaiseCanExecuteChanged();
        discoverCommand?.RaiseCanExecuteChanged();
        planSelectedCommand?.RaiseCanExecuteChanged();
        createCommand?.RaiseCanExecuteChanged();
        loadCurrentPlanIntoFramingCommand?.RaiseCanExecuteChanged();
        recalculateFromFramingCommand?.RaiseCanExecuteChanged();
        openNativeFramingCommand?.RaiseCanExecuteChanged();
    }

    public void Dispose() {
        if (disposed) return;
        disposed = true;
        targetSchedulerDetectionTimer.Stop();
        targetSchedulerDetectionTimer.Tick -= TargetSchedulerDetectionTimer_Tick;
        profileService.ProfileChanged -= ProfileService_ProfileChanged;
        framingIntegration.FramingChanged -= FramingIntegration_FramingChanged;
        framingIntegration.Dispose();
    }
}
