using NINA.Plugin.NightSage.Models;
using NINA.Plugin.NightSage.Services;
using NUnit.Framework;

namespace NINA.Plugin.NightSage.Tests;

[TestFixture]
public class AstronomyMathTests {
    [Test]
    public void AngularSeparation_SamePoint_IsZero() {
        var sep = AstronomyMath.AngularSeparationDeg(5.5, -20, 5.5, -20);
        Assert.That(sep, Is.EqualTo(0).Within(1e-6));
    }

    [Test]
    public void FieldFit_PenalizesOversizedTarget() {
        var setup = new SetupContext { FieldWidthDeg = 1.0, FieldHeightDeg = 0.7 };
        var good = new TargetCandidate { AngularWidthArcmin = 45, AngularHeightArcmin = 30 };
        var huge = new TargetCandidate { AngularWidthArcmin = 180, AngularHeightArcmin = 120 };
        Assert.That(AstronomyMath.FieldFitScore(setup, good), Is.GreaterThan(AstronomyMath.FieldFitScore(setup, huge)));
    }
}
