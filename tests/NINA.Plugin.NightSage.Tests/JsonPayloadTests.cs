using NINA.Plugin.NightSage.Infrastructure;
using NUnit.Framework;
using System.Text.Json;

namespace NINA.Plugin.NightSage.Tests;

[TestFixture]
public class JsonPayloadTests {
    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    public void Double_RejectsNonFiniteStringValues(string value) {
        using var doc = JsonDocument.Parse($$"""{"value":"{{value}}"}""");
        Assert.Throws<InvalidOperationException>(() => JsonPayload.Double(doc.RootElement, "value"));
    }

    [Test]
    public void Int_RejectsOutOfRangeValue() {
        using var doc = JsonDocument.Parse("""{"value":1e30}""");
        Assert.Throws<InvalidOperationException>(() => JsonPayload.Int(doc.RootElement, "value"));
    }
}
