using NINA.Plugin.NightSage.Models;
using NUnit.Framework;

namespace NINA.Plugin.NightSage.Tests;

[TestFixture]
public class FramingModelTests {
    [Test]
    public void Mosaic_MultipliesPerPanelIntegrationIntoProjectTotal() {
        var plan = new ImagingPlan {
            Framing = new FramingSnapshot {
                HorizontalPanels = 2,
                VerticalPanels = 2,
                Panels = new List<FramingPanel> {
                    new() { Index = 1, Name = "P1" },
                    new() { Index = 2, Name = "P2" },
                    new() { Index = 3, Name = "P3" },
                    new() { Index = 4, Name = "P4" }
                }
            },
            Exposures = new List<ExposureRecommendation> {
                new() { Filter = "H", SubSeconds = 600, DesiredCount = 30 },
                new() { Filter = "O", SubSeconds = 600, DesiredCount = 30 }
            }
        };

        Assert.That(plan.PanelCount, Is.EqualTo(4));
        Assert.That(plan.IntegrationPerPanelDisplay, Is.EqualTo("10h 00m"));
        Assert.That(plan.TotalIntegrationDisplay, Is.EqualTo("40h 00m"));
    }

    [Test]
    public void SinglePanel_KeepsProjectAndPanelIntegrationEqual() {
        var plan = new ImagingPlan {
            Exposures = new List<ExposureRecommendation> {
                new() { Filter = "L", SubSeconds = 120, DesiredCount = 60 }
            }
        };

        Assert.That(plan.PanelCount, Is.EqualTo(1));
        Assert.That(plan.IntegrationPerPanelDisplay, Is.EqualTo("2h 00m"));
        Assert.That(plan.TotalIntegrationDisplay, Is.EqualTo("2h 00m"));
    }

    [Test]
    public void FramingFingerprint_ChangesWithPanelGeometry() {
        var a = new FramingSnapshot {
            TargetName = "M31",
            HorizontalPanels = 2,
            VerticalPanels = 1,
            Panels = new List<FramingPanel> {
                new() { Index = 1, Name = "M31 Panel 1", RaHours = 0.7, DecDeg = 41.1, RotationDegrees = 20 },
                new() { Index = 2, Name = "M31 Panel 2", RaHours = 0.8, DecDeg = 41.2, RotationDegrees = 20 }
            }
        };
        var b = new FramingSnapshot {
            TargetName = "M31",
            HorizontalPanels = 2,
            VerticalPanels = 1,
            Panels = new List<FramingPanel> {
                new() { Index = 1, Name = "M31 Panel 1", RaHours = 0.7, DecDeg = 41.1, RotationDegrees = 20 },
                new() { Index = 2, Name = "M31 Panel 2", RaHours = 0.81, DecDeg = 41.2, RotationDegrees = 20 }
            }
        };

        Assert.That(a.Fingerprint, Is.Not.EqualTo(b.Fingerprint));
    }
}
