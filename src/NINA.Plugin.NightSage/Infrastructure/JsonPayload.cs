using System.Text.Json;

namespace NINA.Plugin.NightSage.Infrastructure;

public static class JsonPayload {
    public static JsonDocument ParseObject(string raw) {
        if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("The LLM returned an empty response.");
        var text = raw.Trim();

        if (text.StartsWith("```", StringComparison.Ordinal)) {
            var firstLine = text.IndexOf('\n');
            if (firstLine >= 0) text = text[(firstLine + 1)..];
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0) text = text[..lastFence];
            text = text.Trim();
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) throw new InvalidOperationException("The LLM response did not contain a JSON object.");
        return JsonDocument.Parse(text[start..(end + 1)]);
    }

    public static string String(JsonElement e, string name, string fallback = "") =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? fallback : fallback;

    public static double Double(JsonElement e, string name, double fallback = 0) {
        if (!e.TryGetProperty(name, out var p)) return fallback;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d)) return d;
        return p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out d) ? d : fallback;
    }

    public static int Int(JsonElement e, string name, int fallback = 0) =>
        (int)Math.Round(Double(e, name, fallback));

    public static bool Bool(JsonElement e, string name, bool fallback = false) {
        if (!e.TryGetProperty(name, out var p)) return fallback;
        if (p.ValueKind == JsonValueKind.True) return true;
        if (p.ValueKind == JsonValueKind.False) return false;
        return p.ValueKind == JsonValueKind.String && bool.TryParse(p.GetString(), out var b) ? b : fallback;
    }
}
