using NINA.Astrometry;
using NINA.Plugin.NightSage.Models;
using NINA.Plugin.NightSage.Services;
using NUnit.Framework;

namespace NINA.Plugin.NightSage.Tests;

[TestFixture]
public class AstronomyMathTests {
    [Test]
    public void JulianDate_J2000_IsCorrect() {
        var jd = AstronomyMath.JulianDate(new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        Assert.That(jd, Is.EqualTo(2451545.0).Within(1e-6));
    }

    [Test]
    public void AngularSeparation_SamePoint_IsZero() {
        var sep = AstronomyMath.AngularSeparationDeg(5.5, -20, 5.5, -20);
        Assert.That(sep, Is.EqualTo(0).Within(1e-6));
    }

    [Test]
    public void Altitude_UsesNinaCoordinateTransform() {
        var utc = new DateTime(2026, 9, 28, 22, 0, 0, DateTimeKind.Utc);
        const double raHours = 21.5;
        const double decDeg = 42.0;
        const double latitude = 31.206;
        const double longitude = -7.866;
        const double elevation = 2750;

        var coordinates = new Coordinates(raHours, decDeg, Epoch.J2000, Coordinates.RAType.Hours);
        var expected = coordinates.Transform(
            Angle.ByDegree(latitude),
            Angle.ByDegree(longitude),
            elevation,
            utc).Altitude.Degree;

        var actual = AstronomyMath.AltitudeDeg(
            raHours, decDeg, latitude, longitude, utc, elevation);

        Assert.That(actual, Is.EqualTo(expected).Within(1e-9));
    }

    [Test]
    public void Visibility_UsingNinaAstrometry_ReturnsFiniteValues() {
        var result = AstronomyMath.VisibilityNextDays(
            21.5, 42.0,
            31.206, -7.866, 2750,
            30, 2,
            new DateTime(2026, 9, 28, 18, 0, 0, DateTimeKind.Utc));

        Assert.That(double.IsFinite(result.MaxAltitudeDeg), Is.True);
        Assert.That(double.IsFinite(result.DarkHoursAboveMinimum), Is.True);
        Assert.That(double.IsFinite(result.MoonSeparationAtBestDeg), Is.True);
    }

    [Test]
    public void FieldFit_PenalizesOversizedTarget() {
        var setup = new SetupContext { FieldWidthDeg = 1.0, FieldHeightDeg = 0.7 };
        var good = new TargetCandidate { AngularWidthArcmin = 45, AngularHeightArcmin = 30 };
        var huge = new TargetCandidate { AngularWidthArcmin = 180, AngularHeightArcmin = 120 };
        Assert.That(AstronomyMath.FieldFitScore(setup, good), Is.GreaterThan(AstronomyMath.FieldFitScore(setup, huge)));
    }
}
