using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Plugin.NightSage.Models;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Windows;

namespace NINA.Plugin.NightSage.Services;

public sealed class FramingAssistantIntegration : IDisposable {
    private readonly IFramingAssistantVM framing;
    private readonly IProfileService profileService;
    private readonly IApplicationMediator applicationMediator;
    private readonly List<INotifyPropertyChanged> watchedRectangles = new();
    private INotifyCollectionChanged? watchedCollection;
    private bool suppressChanges;
    private bool disposed;

    public FramingAssistantIntegration(
        IFramingAssistantVM framing,
        IProfileService profileService,
        IApplicationMediator applicationMediator) {

        this.framing = framing;
        this.profileService = profileService;
        this.applicationMediator = applicationMediator;

        if (framing is INotifyPropertyChanged npc) npc.PropertyChanged += Framing_PropertyChanged;
        WireRectangleCollection();
        EmbeddedTemplate = TryCreateEmbeddedTemplate();
    }

    public event EventHandler? FramingChanged;

    public IFramingAssistantVM ViewModel => framing;
    public DataTemplate? EmbeddedTemplate { get; }
    public bool EmbeddedAvailable => EmbeddedTemplate != null;
    public string EmbedStatus => EmbeddedAvailable
        ? "Native N.I.N.A. Framing Assistant embedded — edits are shared with the standard Framing Assistant tab."
        : "Embedded Framing Assistant view is unavailable; use Open native tab. Framing state is still shared.";

    public void Reset() {
        suppressChanges = true;
        try {
            try {
                if (framing.CancelLoadImageCommand?.CanExecute(null) == true)
                    framing.CancelLoadImageCommand.Execute(null);
            } catch { }

            if (framing.HorizontalPanels != 1) framing.HorizontalPanels = 1;
            if (framing.VerticalPanels != 1) framing.VerticalPanels = 1;

            framing.ImageParameter = null!;
            framing.Rectangle = null!;
            framing.CameraRectangles?.Clear();
            framing.DeepSkyObjectSearchVM?.SetTargetNameWithoutSearch(string.Empty);
            framing.DSO = new DeepSkyObject(
                string.Empty,
                new Coordinates(0, 0, Epoch.J2000, Coordinates.RAType.Hours),
                profileService.ActiveProfile.AstrometrySettings.Horizon);

            WireRectangleCollection();
        } finally {
            suppressChanges = false;
        }
    }

    public async Task<bool> LoadTargetAsync(
        string targetName,
        double raHours,
        double decDeg,
        double rotationDegrees,
        bool resetMosaic,
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();
        suppressChanges = true;
        try {
            if (resetMosaic) {
                framing.HorizontalPanels = 1;
                framing.VerticalPanels = 1;
            }

            var coordinates = new Coordinates(raHours, decDeg, Epoch.J2000, Coordinates.RAType.Hours);
            var dso = new DeepSkyObject(targetName, coordinates, profileService.ActiveProfile.AstrometrySettings.Horizon) {
                RotationPositionAngle = NormalizeDegrees(rotationDegrees)
            };

            var ok = await framing.SetCoordinates(dso).ConfigureAwait(true);
            WireRectangleCollection();
            return ok;
        } finally {
            suppressChanges = false;
        }
    }

    public FramingSnapshot Capture() {
        var panels = new List<FramingPanel>();
        var index = 1;

        foreach (var rect in framing.CameraRectangles ?? new AsyncObservableCollection<FramingRectangle>()) {
            if (rect?.Coordinates == null) continue;
            panels.Add(new FramingPanel {
                Index = rect.Id > 0 ? rect.Id : index,
                Name = string.IsNullOrWhiteSpace(rect.Name)
                    ? $"{framing.DSO?.Name ?? "Target"} Panel {index}"
                    : rect.Name,
                RaHours = rect.Coordinates.RA,
                DecDeg = rect.Coordinates.Dec,
                RotationDegrees = NormalizeDegrees(rect.DSOPositionAngle)
            });
            index++;
        }

        var center = framing.Rectangle?.Coordinates ?? framing.DSO?.Coordinates;
        var overlapUnit = string.IsNullOrWhiteSpace(framing.SelectedOverlapUnit) ? "%" : framing.SelectedOverlapUnit;
        var overlapValue = overlapUnit == "%" ? framing.OverlapPercentage * 100.0 : framing.OverlapPixels;

        return new FramingSnapshot {
            TargetName = framing.DSO?.Name ?? "",
            Source = framing.FramingAssistantSource.ToString(),
            HorizontalPanels = Math.Max(1, framing.HorizontalPanels),
            VerticalPanels = Math.Max(1, framing.VerticalPanels),
            OverlapValue = overlapValue,
            OverlapUnit = overlapUnit,
            CenterRaHours = center?.RA ?? 0,
            CenterDecDeg = center?.Dec ?? 0,
            Panels = panels
        };
    }

    public void OpenNativeFramingAssistant() => applicationMediator.ChangeTab(ApplicationTab.FRAMINGASSISTANT);

    private static DataTemplate? TryCreateEmbeddedTemplate() {
        try {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("NINA.View.FramingAssistantView", false))
                .FirstOrDefault(t => t != null);

            if (type == null) {
                Logger.Warning("NightSage: NINA.View.FramingAssistantView type not found; embedded framing disabled.");
                return null;
            }

#pragma warning disable CS0618
            var factory = new FrameworkElementFactory(type);
            return new DataTemplate { VisualTree = factory };
#pragma warning restore CS0618
        } catch (Exception ex) {
            Logger.Warning($"NightSage: could not prepare embedded Framing Assistant template: {ex.Message}");
            return null;
        }
    }

    private void Framing_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(IFramingAssistantVM.CameraRectangles)) WireRectangleCollection();

        if (e.PropertyName is nameof(IFramingAssistantVM.HorizontalPanels)
            or nameof(IFramingAssistantVM.VerticalPanels)
            or nameof(IFramingAssistantVM.OverlapPercentage)
            or nameof(IFramingAssistantVM.OverlapPixels)
            or nameof(IFramingAssistantVM.SelectedOverlapUnit)
            or nameof(IFramingAssistantVM.Rectangle)
            or nameof(IFramingAssistantVM.DSO)
            or nameof(IFramingAssistantVM.FocalLength)
            or nameof(IFramingAssistantVM.CameraWidth)
            or nameof(IFramingAssistantVM.CameraHeight)) {
            RaiseChanged();
        }
    }

    private void WireRectangleCollection() {
        if (watchedCollection != null) watchedCollection.CollectionChanged -= CameraRectangles_CollectionChanged;
        foreach (var rectangle in watchedRectangles) rectangle.PropertyChanged -= Rectangle_PropertyChanged;
        watchedRectangles.Clear();

        watchedCollection = framing.CameraRectangles as INotifyCollectionChanged;
        if (watchedCollection != null) watchedCollection.CollectionChanged += CameraRectangles_CollectionChanged;

        if (framing.CameraRectangles == null) return;
        foreach (var rectangle in framing.CameraRectangles) {
            if (rectangle is INotifyPropertyChanged npc) {
                watchedRectangles.Add(npc);
                npc.PropertyChanged += Rectangle_PropertyChanged;
            }
        }
    }

    private void CameraRectangles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        WireRectangleCollection();
        RaiseChanged();
    }

    private void Rectangle_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName is nameof(FramingRectangle.Coordinates)
            or nameof(FramingRectangle.DSOPositionAngle)
            or nameof(FramingRectangle.Rotation)
            or nameof(FramingRectangle.Name)) {
            RaiseChanged();
        }
    }

    private void RaiseChanged() {
        if (suppressChanges || disposed) return;
        FramingChanged?.Invoke(this, EventArgs.Empty);
    }

    private static double NormalizeDegrees(double value) {
        value %= 360;
        return value < 0 ? value + 360 : value;
    }

    public void Dispose() {
        if (disposed) return;
        disposed = true;

        if (framing is INotifyPropertyChanged npc) npc.PropertyChanged -= Framing_PropertyChanged;
        if (watchedCollection != null) watchedCollection.CollectionChanged -= CameraRectangles_CollectionChanged;
        foreach (var rectangle in watchedRectangles) rectangle.PropertyChanged -= Rectangle_PropertyChanged;
        watchedRectangles.Clear();
    }
}
