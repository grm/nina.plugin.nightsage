using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NINA.Plugin.NightSage.Providers;

public abstract class HttpLlmProvider : ILLMProvider {
    protected readonly HttpClient Http;
    protected readonly string ApiKey;
    protected readonly string Model;
    protected readonly string Endpoint;

    protected HttpLlmProvider(HttpClient http, string apiKey, string model, string endpoint) {
        Http = http;
        ApiKey = apiKey ?? "";
        Model = model;
        Endpoint = endpoint;
    }

    public abstract string Name { get; }
    public abstract Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);

    protected static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    protected async Task<HttpResponseMessage> SendWithTransientRetryAsync(Func<HttpRequestMessage> requestFactory, CancellationToken ct) {
        for (var attempt = 0; attempt < 2; attempt++) {
            try {
                using var request = requestFactory();
                var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
                if (attempt == 0 && IsTransient(response.StatusCode)) {
                    response.Dispose();
                    await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                    continue;
                }
                return response;
            } catch (TaskCanceledException ex) when (!ct.IsCancellationRequested) {
                if (attempt == 0) {
                    await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                    continue;
                }
                var seconds = Http.Timeout == Timeout.InfiniteTimeSpan ? "configured" : $"{Http.Timeout.TotalSeconds:0}";
                throw new TimeoutException($"LLM provider did not respond within {seconds} seconds. The provider may be temporarily slow or rate-limited; retry the request.", ex);
            }
        }
        throw new InvalidOperationException("LLM request failed after retry.");
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout ||
        (int)status >= 500;

    protected static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct) {
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            var safe = text.Length > 1000 ? text[..1000] : text;
            throw new InvalidOperationException($"LLM request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {safe}");
        }
        return JsonDocument.Parse(text);
    }
}

public sealed class OpenAiProvider : HttpLlmProvider {
    public OpenAiProvider(HttpClient http, string apiKey, string model, string? endpoint = null)
        : base(http, apiKey, string.IsNullOrWhiteSpace(model) ? "gpt-5.6-terra" : model,
              string.IsNullOrWhiteSpace(endpoint) ? "https://api.openai.com/v1/responses" : endpoint!) { }
    public override string Name => "OpenAI";
    public override async Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) {
        using var response = await SendWithTransientRetryAsync(() => {
            var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            request.Content = JsonBody(new { model = Model, instructions = systemPrompt, input = userPrompt, max_output_tokens = 7000 });
            return request;
        }, cancellationToken).ConfigureAwait(false);
        using var doc = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;
        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String) return outputText.GetString() ?? "";
        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array) {
            foreach (var item in output.EnumerateArray()) {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
                foreach (var part in content.EnumerateArray())
                    if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String) return text.GetString() ?? "";
            }
        }
        throw new InvalidOperationException("OpenAI returned no text output.");
    }
}

public sealed class AnthropicProvider : HttpLlmProvider {
    public AnthropicProvider(HttpClient http, string apiKey, string model, string? endpoint = null)
        : base(http, apiKey, string.IsNullOrWhiteSpace(model) ? "claude-sonnet-4-5" : model,
              string.IsNullOrWhiteSpace(endpoint) ? "https://api.anthropic.com/v1/messages" : endpoint!) { }
    public override string Name => "Anthropic";
    public override async Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) {
        using var response = await SendWithTransientRetryAsync(() => {
            var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.TryAddWithoutValidation("x-api-key", ApiKey);
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            request.Content = JsonBody(new { model = Model, max_tokens = 7000, system = systemPrompt, messages = new[] { new { role = "user", content = userPrompt } } });
            return request;
        }, cancellationToken).ConfigureAwait(false);
        using var doc = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        if (doc.RootElement.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            foreach (var part in content.EnumerateArray())
                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String) return text.GetString() ?? "";
        throw new InvalidOperationException("Anthropic returned no text output.");
    }
}

public sealed class GeminiProvider : HttpLlmProvider {
    public GeminiProvider(HttpClient http, string apiKey, string model, string? endpoint = null)
        : base(http, apiKey, string.IsNullOrWhiteSpace(model) ? "gemini-2.5-pro" : model, endpoint ?? "") { }
    public override string Name => "Google Gemini";
    public override async Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) {
        var baseUrl = string.IsNullOrWhiteSpace(Endpoint)
            ? $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(Model)}:generateContent"
            : Endpoint;
        var separator = baseUrl.Contains('?') ? "&" : "?";
        var url = string.IsNullOrWhiteSpace(ApiKey) ? baseUrl : baseUrl + separator + "key=" + Uri.EscapeDataString(ApiKey);

        using var response = await SendWithTransientRetryAsync(() => new HttpRequestMessage(HttpMethod.Post, url) {
            Content = JsonBody(new {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
                generationConfig = new { responseMimeType = "application/json", maxOutputTokens = 7000 }
            })
        }, cancellationToken).ConfigureAwait(false);

        using var doc = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        var candidates = doc.RootElement.GetProperty("candidates");
        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        return parts[0].GetProperty("text").GetString() ?? "";
    }
}

public sealed class OpenAiCompatibleProvider : HttpLlmProvider {
    public OpenAiCompatibleProvider(HttpClient http, string apiKey, string model, string? endpoint = null)
        : base(http, apiKey, model, NormalizeEndpoint(string.IsNullOrWhiteSpace(endpoint) ? "http://localhost:11434/v1" : endpoint!)) { }
    public override string Name => "OpenAI-compatible";
    private static string NormalizeEndpoint(string endpoint) {
        endpoint = endpoint.TrimEnd('/');
        return endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) ? endpoint : endpoint + "/chat/completions";
    }
    public override async Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(Model)) throw new InvalidOperationException("A model name is required for the OpenAI-compatible provider.");
        using var response = await SendWithTransientRetryAsync(() => {
            var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            if (!string.IsNullOrWhiteSpace(ApiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            request.Content = JsonBody(new {
                model = Model,
                messages = new object[] { new { role = "system", content = systemPrompt }, new { role = "user", content = userPrompt } },
                temperature = 0.2
            });
            return request;
        }, cancellationToken).ConfigureAwait(false);
        using var doc = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
               ?? throw new InvalidOperationException("Compatible endpoint returned no text.");
    }
}
