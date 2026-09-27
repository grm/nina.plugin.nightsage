using NINA.Plugin.NightSage.Models;

namespace NINA.Plugin.NightSage.Services;

public static class AstronomyMath {
    public static double AltitudeDeg(double raHours, double decDeg, double latitudeDeg, double longitudeDeg, DateTime utc) {
        var lstDeg = NormalizeDeg(GmstDeg(utc) + longitudeDeg);
        var hourAngleDeg = NormalizeSignedDeg(lstDeg - raHours * 15.0);
        var lat = Rad(latitudeDeg);
        var dec = Rad(decDeg);
        var ha = Rad(hourAngleDeg);
        var sinAlt = Math.Sin(lat) * Math.Sin(dec) + Math.Cos(lat) * Math.Cos(dec) * Math.Cos(ha);
        return Deg(Math.Asin(Clamp(sinAlt, -1, 1)));
    }

    public static (double raHours, double decDeg) SunEquatorial(DateTime utc) {
        var d = JulianDate(utc) - 2451545.0;
        var g = Rad(NormalizeDeg(357.529 + 0.98560028 * d));
        var q = NormalizeDeg(280.459 + 0.98564736 * d);
        var lambda = Rad(NormalizeDeg(q + 1.915 * Math.Sin(g) + 0.020 * Math.Sin(2 * g)));
        var epsilon = Rad(23.439 - 0.00000036 * d);
        var ra = Math.Atan2(Math.Cos(epsilon) * Math.Sin(lambda), Math.Cos(lambda));
        var dec = Math.Asin(Math.Sin(epsilon) * Math.Sin(lambda));
        return (NormalizeHours(Deg(ra) / 15.0), Deg(dec));
    }

    // Low-precision lunar position adequate for planning/avoidance scoring.
    public static (double raHours, double decDeg) MoonEquatorial(DateTime utc) {
        var d = JulianDate(utc) - 2451543.5;
        var n = Rad(NormalizeDeg(125.1228 - 0.0529538083 * d));
        var i = Rad(5.1454);
        var w = Rad(NormalizeDeg(318.0634 + 0.1643573223 * d));
        var a = 60.2666;
        var e = 0.054900;
        var m = Rad(NormalizeDeg(115.3654 + 13.0649929509 * d));

        var eAnom = m + e * Math.Sin(m) * (1.0 + e * Math.Cos(m));
        for (var k = 0; k < 4; k++) {
            eAnom -= (eAnom - e * Math.Sin(eAnom) - m) / (1 - e * Math.Cos(eAnom));
        }

        var xv = a * (Math.Cos(eAnom) - e);
        var yv = a * Math.Sqrt(1 - e * e) * Math.Sin(eAnom);
        var v = Math.Atan2(yv, xv);
        var r = Math.Sqrt(xv * xv + yv * yv);

        var xh = r * (Math.Cos(n) * Math.Cos(v + w) - Math.Sin(n) * Math.Sin(v + w) * Math.Cos(i));
        var yh = r * (Math.Sin(n) * Math.Cos(v + w) + Math.Cos(n) * Math.Sin(v + w) * Math.Cos(i));
        var zh = r * (Math.Sin(v + w) * Math.Sin(i));

        var lon = Math.Atan2(yh, xh);
        var lat = Math.Atan2(zh, Math.Sqrt(xh * xh + yh * yh));

        var sun = SunEcliptic(utc);
        var lm = NormalizeDeg(Deg(n + w + m));
        var ls = sun.longitudeDeg;
        var mm = Deg(m);
        var ms = sun.meanAnomalyDeg;
        var dd = NormalizeSignedDeg(lm - ls);
        var f = NormalizeSignedDeg(lm - Deg(n));

        var lonDeg = Deg(lon)
            - 1.274 * Math.Sin(Rad(mm - 2 * dd))
            + 0.658 * Math.Sin(Rad(2 * dd))
            - 0.186 * Math.Sin(Rad(ms))
            - 0.059 * Math.Sin(Rad(2 * mm - 2 * dd))
            - 0.057 * Math.Sin(Rad(mm - 2 * dd + ms))
            + 0.053 * Math.Sin(Rad(mm + 2 * dd))
            + 0.046 * Math.Sin(Rad(2 * dd - ms))
            + 0.041 * Math.Sin(Rad(mm - ms))
            - 0.035 * Math.Sin(Rad(dd))
            - 0.031 * Math.Sin(Rad(mm + ms))
            - 0.015 * Math.Sin(Rad(2 * f - 2 * dd))
            + 0.011 * Math.Sin(Rad(mm - 4 * dd));

        var latDeg = Deg(lat)
            - 0.173 * Math.Sin(Rad(f - 2 * dd))
            - 0.055 * Math.Sin(Rad(mm - f - 2 * dd))
            - 0.046 * Math.Sin(Rad(mm + f - 2 * dd))
            + 0.033 * Math.Sin(Rad(f + 2 * dd))
            + 0.017 * Math.Sin(Rad(2 * mm + f));

        lon = Rad(lonDeg);
        lat = Rad(latDeg);
        var eps = Rad(23.4393 - 3.563e-7 * d);

        var xe = Math.Cos(lon) * Math.Cos(lat);
        var ye = Math.Sin(lon) * Math.Cos(lat) * Math.Cos(eps) - Math.Sin(lat) * Math.Sin(eps);
        var ze = Math.Sin(lon) * Math.Cos(lat) * Math.Sin(eps) + Math.Sin(lat) * Math.Cos(eps);

        var ra = Math.Atan2(ye, xe);
        var dec = Math.Atan2(ze, Math.Sqrt(xe * xe + ye * ye));
        return (NormalizeHours(Deg(ra) / 15.0), Deg(dec));
    }

    public static double AngularSeparationDeg(double ra1Hours, double dec1Deg, double ra2Hours, double dec2Deg) {
        var ra1 = Rad(ra1Hours * 15);
        var ra2 = Rad(ra2Hours * 15);
        var d1 = Rad(dec1Deg);
        var d2 = Rad(dec2Deg);
        var cos = Math.Sin(d1) * Math.Sin(d2) + Math.Cos(d1) * Math.Cos(d2) * Math.Cos(ra1 - ra2);
        return Deg(Math.Acos(Clamp(cos, -1, 1)));
    }

    public static VisibilitySummary VisibilityNextDays(
        double raHours, double decDeg, double latitudeDeg, double longitudeDeg,
        double minimumAltitudeDeg, int days, DateTime? startUtc = null) {

        var start = (startUtc ?? DateTime.UtcNow).ToUniversalTime();
        var end = start.AddDays(Math.Max(1, days));
        var thresholds = new[] { -18.0, -12.0, -6.0 };
        foreach (var sunThreshold in thresholds) {
            var result = VisibilityWithSunThreshold(raHours, decDeg, latitudeDeg, longitudeDeg,
                minimumAltitudeDeg, start, end, sunThreshold);
            if (result.DarkHoursAboveMinimum > 0 || sunThreshold == thresholds[^1]) return result;
        }
        return new VisibilitySummary();
    }

    private static VisibilitySummary VisibilityWithSunThreshold(
        double raHours, double decDeg, double latitudeDeg, double longitudeDeg,
        double minAltitude, DateTime start, DateTime end, double sunThreshold) {

        var result = new VisibilitySummary { MaxAltitudeDeg = -90, BestUtc = start, MoonSeparationAtBestDeg = 180 };
        var usableSamples = 0;
        var bestQuality = double.NegativeInfinity;

        for (var t = start; t <= end; t = t.AddMinutes(10)) {
            var sun = SunEquatorial(t);
            var sunAlt = AltitudeDeg(sun.raHours, sun.decDeg, latitudeDeg, longitudeDeg, t);
            if (sunAlt > sunThreshold) continue;

            var alt = AltitudeDeg(raHours, decDeg, latitudeDeg, longitudeDeg, t);
            if (alt > result.MaxAltitudeDeg) result.MaxAltitudeDeg = alt;
            if (alt < minAltitude) continue;

            usableSamples++;
            var moon = MoonEquatorial(t);
            var moonSep = AngularSeparationDeg(raHours, decDeg, moon.raHours, moon.decDeg);
            var quality = alt + Math.Min(30, moonSep / 3.0);
            if (quality > bestQuality) {
                bestQuality = quality;
                result.BestUtc = t;
                result.MoonSeparationAtBestDeg = moonSep;
            }
        }

        if (result.MaxAltitudeDeg < -89) result.MaxAltitudeDeg = 0;
        result.DarkHoursAboveMinimum = usableSamples * (10.0 / 60.0);
        return result;
    }

    public static double CandidateScore(SetupContext setup, TargetCandidate candidate, double minAltitude) {
        var vis = candidate.Visibility;
        var visibility = Clamp(vis.DarkHoursAboveMinimum / 12.0, 0, 1) * 40.0;
        var altitude = Clamp((vis.MaxAltitudeDeg - minAltitude) / Math.Max(1, 90 - minAltitude), 0, 1) * 25.0;
        var fit = FieldFitScore(setup, candidate) * 25.0;
        var model = Clamp(candidate.ModelScore / 100.0, 0, 1) * 10.0;
        return Math.Round(visibility + altitude + fit + model, 1);
    }

    public static double FieldFitScore(SetupContext setup, TargetCandidate candidate) {
        if (setup.FieldWidthDeg <= 0 || setup.FieldHeightDeg <= 0 ||
            candidate.AngularWidthArcmin <= 0 || candidate.AngularHeightArcmin <= 0) return 0.6;

        var fw = setup.FieldWidthDeg * 60.0;
        var fh = setup.FieldHeightDeg * 60.0;
        var tw = Math.Max(candidate.AngularWidthArcmin, candidate.AngularHeightArcmin);
        var th = Math.Min(candidate.AngularWidthArcmin, candidate.AngularHeightArcmin);
        var landscapeFit = Math.Max(tw / fw, th / fh);
        var portraitFit = Math.Max(tw / fh, th / fw);
        var ratio = Math.Min(landscapeFit, portraitFit);

        if (ratio <= 0.85 && ratio >= 0.18) return 1.0;
        if (ratio < 0.18) return Clamp(ratio / 0.18, 0.3, 1.0);
        if (ratio <= 1.05) return 0.7;
        return Clamp(1.4 - ratio, 0, 0.6);
    }

    private static (double longitudeDeg, double meanAnomalyDeg) SunEcliptic(DateTime utc) {
        var d = JulianDate(utc) - 2451543.5;
        var w = 282.9404 + 4.70935e-5 * d;
        var e = 0.016709 - 1.151e-9 * d;
        var m = NormalizeDeg(356.0470 + 0.9856002585 * d);
        var mr = Rad(m);
        var eAnom = mr + e * Math.Sin(mr) * (1 + e * Math.Cos(mr));
        var xv = Math.Cos(eAnom) - e;
        var yv = Math.Sqrt(1 - e * e) * Math.Sin(eAnom);
        var v = Deg(Math.Atan2(yv, xv));
        return (NormalizeDeg(v + w), m);
    }

    public static double JulianDate(DateTime utc) {
        utc = utc.ToUniversalTime();
        var y = utc.Year;
        var m = utc.Month;
        var day = utc.Day + (utc.Hour + (utc.Minute + (utc.Second + utc.Millisecond / 1000.0) / 60.0) / 60.0) / 24.0;
        if (m <= 2) { y--; m += 12; }
        var a = Math.Floor(y / 100.0);
        var b = 2 - a + Math.Floor(a / 4.0);
        return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + day + b - 1524.5;
    }

    private static double GmstDeg(DateTime utc) {
        var jd = JulianDate(utc);
        var t = (jd - 2451545.0) / 36525.0;
        return NormalizeDeg(280.46061837 + 360.98564736629 * (jd - 2451545.0) + 0.000387933 * t * t - t * t * t / 38710000.0);
    }

    private static double Rad(double deg) => deg * Math.PI / 180.0;
    private static double Deg(double rad) => rad * 180.0 / Math.PI;
    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
    private static double NormalizeDeg(double deg) { deg %= 360; if (deg < 0) deg += 360; return deg; }
    private static double NormalizeSignedDeg(double deg) { deg = NormalizeDeg(deg); return deg > 180 ? deg - 360 : deg; }
    private static double NormalizeHours(double h) { h %= 24; if (h < 0) h += 24; return h; }
}
