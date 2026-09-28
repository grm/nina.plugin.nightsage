using NINA.Astrometry;
using NINA.Core.Utility;
using NINA.Plugin.NightSage.Models;

namespace NINA.Plugin.NightSage.Services;

public static class AstronomyMath {
    public static double AltitudeDeg(
        double raHours,
        double decDeg,
        double latitudeDeg,
        double longitudeDeg,
        DateTime utc,
        double elevationM = 0) {

        var coordinates = new Coordinates(raHours, decDeg, Epoch.J2000, Coordinates.RAType.Hours);
        var topocentric = coordinates.Transform(
            Angle.ByDegree(latitudeDeg),
            Angle.ByDegree(longitudeDeg),
            elevationM,
            utc.ToUniversalTime());

        return topocentric.Altitude.Degree;
    }

    public static double AngularSeparationDeg(double ra1Hours, double dec1Deg, double ra2Hours, double dec2Deg) {
        var ra1 = AstroUtil.ToRadians(AstroUtil.HoursToDegrees(ra1Hours));
        var ra2 = AstroUtil.ToRadians(AstroUtil.HoursToDegrees(ra2Hours));
        var d1 = AstroUtil.ToRadians(dec1Deg);
        var d2 = AstroUtil.ToRadians(dec2Deg);
        var cosine = Math.Sin(d1) * Math.Sin(d2) + Math.Cos(d1) * Math.Cos(d2) * Math.Cos(ra1 - ra2);
        return AstroUtil.ToDegree(Math.Acos(Math.Clamp(cosine, -1, 1)));
    }

    public static VisibilitySummary VisibilityNextDays(
        double raHours,
        double decDeg,
        double latitudeDeg,
        double longitudeDeg,
        double minimumAltitudeDeg,
        int days,
        DateTime? startUtc = null) =>
        VisibilityNextDays(
            raHours,
            decDeg,
            latitudeDeg,
            longitudeDeg,
            0,
            minimumAltitudeDeg,
            days,
            startUtc);

    public static VisibilitySummary VisibilityNextDays(
        double raHours,
        double decDeg,
        double latitudeDeg,
        double longitudeDeg,
        double elevationM,
        double minimumAltitudeDeg,
        int days,
        DateTime? startUtc = null) {

        var start = (startUtc ?? DateTime.UtcNow).ToUniversalTime();
        var end = start.AddDays(Math.Max(1, days));
        var observer = new ObserverInfo {
            Latitude = latitudeDeg,
            Longitude = longitudeDeg,
            Elevation = elevationM
        };

        var thresholds = new[] { -18.0, -12.0, -6.0 };
        foreach (var sunThreshold in thresholds) {
            var result = VisibilityWithSunThreshold(
                raHours,
                decDeg,
                observer,
                minimumAltitudeDeg,
                start,
                end,
                sunThreshold);

            if (result.DarkHoursAboveMinimum > 0 || sunThreshold == thresholds[^1])
                return result;
        }

        return new VisibilitySummary();
    }

    private static VisibilitySummary VisibilityWithSunThreshold(
        double raHours,
        double decDeg,
        ObserverInfo observer,
        double minAltitude,
        DateTime start,
        DateTime end,
        double sunThreshold) {

        var result = new VisibilitySummary {
            MaxAltitudeDeg = -90,
            BestUtc = start,
            MoonSeparationAtBestDeg = 180
        };

        var usableSamples = 0;
        var bestQuality = double.NegativeInfinity;

        for (var t = start; t <= end; t = t.AddMinutes(10)) {
            var sunAltitude = AstroUtil.GetSunAltitude(t, observer);
            if (sunAltitude > sunThreshold) continue;

            var altitude = AltitudeDeg(
                raHours,
                decDeg,
                observer.Latitude,
                observer.Longitude,
                t,
                observer.Elevation);

            if (altitude > result.MaxAltitudeDeg)
                result.MaxAltitudeDeg = altitude;

            if (altitude < minAltitude)
                continue;

            usableSamples++;

            var moonSeparation = MoonSeparationDeg(raHours, decDeg, t, observer);
            var quality = altitude + Math.Min(30, moonSeparation / 3.0);
            if (quality > bestQuality) {
                bestQuality = quality;
                result.BestUtc = t;
                result.MoonSeparationAtBestDeg = moonSeparation;
            }
        }

        if (result.MaxAltitudeDeg < -89)
            result.MaxAltitudeDeg = 0;

        result.DarkHoursAboveMinimum = usableSamples * (10.0 / 60.0);
        return result;
    }

    private static double MoonSeparationDeg(
        double targetRaHours,
        double targetDecDeg,
        DateTime utc,
        ObserverInfo observer) {

        var moon = AstroUtil.GetMoonPosition(utc, AstroUtil.GetJulianDate(utc), observer);
        var fixedClock = new FixedDateTime(utc);
        var targetJNow = new Coordinates(
            Angle.ByHours(targetRaHours),
            Angle.ByDegree(targetDecDeg),
            Epoch.J2000,
            fixedClock).Transform(Epoch.JNOW);

        return AngularSeparationDeg(targetJNow.RA, targetJNow.Dec, moon.RA, moon.Dec);
    }

    public static double CandidateScore(SetupContext setup, TargetCandidate candidate, double minAltitude) {
        var vis = candidate.Visibility;
        var visibility = Math.Clamp(vis.DarkHoursAboveMinimum / 12.0, 0, 1) * 40.0;
        var altitude = Math.Clamp((vis.MaxAltitudeDeg - minAltitude) / Math.Max(1, 90 - minAltitude), 0, 1) * 25.0;
        var fit = FieldFitScore(setup, candidate) * 25.0;
        var model = Math.Clamp(candidate.ModelScore / 100.0, 0, 1) * 10.0;
        return Math.Round(visibility + altitude + fit + model, 1);
    }

    public static double FieldFitScore(SetupContext setup, TargetCandidate candidate) {
        if (setup.FieldWidthDeg <= 0 || setup.FieldHeightDeg <= 0 ||
            candidate.AngularWidthArcmin <= 0 || candidate.AngularHeightArcmin <= 0)
            return 0.6;

        var fw = setup.FieldWidthDeg * 60.0;
        var fh = setup.FieldHeightDeg * 60.0;
        var tw = Math.Max(candidate.AngularWidthArcmin, candidate.AngularHeightArcmin);
        var th = Math.Min(candidate.AngularWidthArcmin, candidate.AngularHeightArcmin);
        var landscapeFit = Math.Max(tw / fw, th / fh);
        var portraitFit = Math.Max(tw / fh, th / fw);
        var ratio = Math.Min(landscapeFit, portraitFit);

        if (ratio <= 0.85 && ratio >= 0.18) return 1.0;
        if (ratio < 0.18) return Math.Clamp(ratio / 0.18, 0.3, 1.0);
        if (ratio <= 1.05) return 0.7;
        return Math.Clamp(1.4 - ratio, 0, 0.6);
    }

    public static double JulianDate(DateTime utc) => AstroUtil.GetJulianDate(utc.ToUniversalTime());

    private sealed class FixedDateTime : ICustomDateTime {
        public FixedDateTime(DateTime utc) {
            UtcNow = utc.ToUniversalTime();
            Now = UtcNow.ToLocalTime();
        }

        public DateTime Now { get; }
        public DateTime UtcNow { get; }
    }
}
