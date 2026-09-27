using NINA.Plugin.NightSage.Models;
using System.Globalization;
using System.Net.Http;
using System.Xml.Linq;

namespace NINA.Plugin.NightSage.Services;

public interface ITargetResolver {
    Task<ResolvedTarget> ResolveAsync(string query, CancellationToken cancellationToken);
}

public sealed class SesameTargetResolver : ITargetResolver {
    private readonly HttpClient http;

    public SesameTargetResolver(HttpClient? httpClient = null) {
        http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        if (!http.DefaultRequestHeaders.UserAgent.Any()) {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("NightSage/0.1");
        }
    }

    public async Task<ResolvedTarget> ResolveAsync(string query, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Target name is required.", nameof(query));

        var url = "https://cds.unistra.fr/cgi-bin/nph-sesame/-oxp/SNV?" + Uri.EscapeDataString(query.Trim());
        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = XDocument.Parse(xml);

        var resolver = doc.Descendants("Resolver").FirstOrDefault()
            ?? throw new InvalidOperationException($"CDS Sesame could not resolve '{query}'.");

        var raText = resolver.Descendants("jradeg").FirstOrDefault()?.Value;
        var decText = resolver.Descendants("jdedeg").FirstOrDefault()?.Value;
        if (!double.TryParse(raText, NumberStyles.Float, CultureInfo.InvariantCulture, out var raDeg) ||
            !double.TryParse(decText, NumberStyles.Float, CultureInfo.InvariantCulture, out var decDeg)) {
            throw new InvalidOperationException($"CDS Sesame returned no J2000 coordinates for '{query}'.");
        }

        var oname = resolver.Descendants("oname").FirstOrDefault()?.Value;
        var service = resolver.Attribute("name")?.Value ?? "CDS Sesame";

        return new ResolvedTarget {
            Query = query.Trim(),
            CanonicalName = string.IsNullOrWhiteSpace(oname) ? query.Trim() : oname.Trim(),
            RaHours = NormalizeHours(raDeg / 15.0),
            DecDeg = decDeg,
            Resolver = service
        };
    }

    private static double NormalizeHours(double value) {
        value %= 24;
        if (value < 0) value += 24;
        return value;
    }
}
