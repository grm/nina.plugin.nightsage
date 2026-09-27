using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin.NightSage.Infrastructure;
using NINA.Plugin.NightSage.Models;
using NINA.Profile.Interfaces;

namespace NINA.Plugin.NightSage.Services;

public sealed class EquipmentContextService {
    private readonly IProfileService profileService;
    private readonly ICameraMediator cameraMediator;

    public EquipmentContextService(IProfileService profileService, ICameraMediator cameraMediator) {
        this.profileService = profileService;
        this.cameraMediator = cameraMediator;
    }

    public SetupContext Capture() {
        var p = profileService.ActiveProfile ?? throw new InvalidOperationException("No active N.I.N.A. profile.");
        var cameraSettings = p.CameraSettings;
        var telescopeSettings = p.TelescopeSettings;
        var astro = p.AstrometrySettings;

        var cameraInfo = cameraMediator.GetInfo();
        var connected = cameraInfo?.Connected == true;

        var pixelSize = connected && cameraInfo.PixelSize > 0 ? cameraInfo.PixelSize : cameraSettings.PixelSize;
        var x = connected ? cameraInfo.XSize : 0;
        var y = connected ? cameraInfo.YSize : 0;
        var focal = telescopeSettings.FocalLength;
        var fovW = FieldOfViewDeg(x, pixelSize, focal);
        var fovH = FieldOfViewDeg(y, pixelSize, focal);
        var scale = focal > 0 && pixelSize > 0 ? 206.265 * pixelSize / focal : 0;

        var filters = p.FilterWheelSettings?.FilterWheelFilters?
            .Where(f => !string.IsNullOrWhiteSpace(f.Name))
            .OrderBy(f => f.Position)
            .Select(f => f.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        return new SetupContext {
            ProfileId = p.Id.ToString(),
            ProfileName = p.Name ?? p.Id.ToString(),
            TelescopeName = FirstNonEmpty(telescopeSettings.Name, telescopeSettings.LastDeviceName, telescopeSettings.Id),
            CameraName = connected
                ? FirstNonEmpty(cameraInfo.DisplayName, cameraSettings.LastDeviceName, cameraSettings.Id)
                : FirstNonEmpty(cameraSettings.LastDeviceName, cameraSettings.Id),
            FocalLengthMm = double.IsFinite(focal) ? focal : 0,
            FocalRatio = double.IsFinite(telescopeSettings.FocalRatio) ? telescopeSettings.FocalRatio : 0,
            PixelSizeMicrons = pixelSize,
            SensorWidthPixels = x,
            SensorHeightPixels = y,
            FieldWidthDeg = fovW,
            FieldHeightDeg = fovH,
            ImageScaleArcsecPerPixel = scale,
            CameraConnected = connected,
            DefaultGain = ReadInt(cameraInfo, "DefaultGain") ?? cameraSettings.Gain,
            DefaultOffset = ReadInt(cameraInfo, "DefaultOffset") ?? cameraSettings.Offset,
            GainMin = ReadInt(cameraInfo, "GainMin"),
            GainMax = ReadInt(cameraInfo, "GainMax"),
            OffsetMin = ReadInt(cameraInfo, "OffsetMin"),
            OffsetMax = ReadInt(cameraInfo, "OffsetMax"),
            ReadoutMode = cameraSettings.ReadoutMode,
            LatitudeDeg = astro.Latitude,
            LongitudeDeg = astro.Longitude,
            ElevationM = astro.Elevation,
            BayerPattern = cameraSettings.BayerPattern.ToString(),
            Filters = filters
        };
    }

    private static int? ReadInt(object? obj, string name) {
        var value = ReflectionUtil.Get(obj, name);
        if (value == null) return null;
        try { return Convert.ToInt32(value); } catch { return null; }
    }

    private static double FieldOfViewDeg(int pixels, double pixelSizeMicrons, double focalLengthMm) {
        if (pixels <= 0 || pixelSizeMicrons <= 0 || focalLengthMm <= 0) return 0;
        var sensorMm = pixels * pixelSizeMicrons / 1000.0;
        return 2 * Math.Atan(sensorMm / (2 * focalLengthMm)) * 180.0 / Math.PI;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Unknown";
}
