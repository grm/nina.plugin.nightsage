using System.Net.Http;
using NINA.Plugin.NightSage.Infrastructure;

namespace NINA.Plugin.NightSage.Providers;

public static class LlmProviderFactory {
    public static ILLMProvider Create(NightSageSettings settings, HttpClient? httpClient = null) {
        var http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var key = NightSageSettingsStore.UnprotectSecret(settings.ApiKeyProtected);
        return settings.Provider.Trim().ToLowerInvariant() switch {
            "openai" => new OpenAiProvider(http, key, settings.Model, settings.Endpoint),
            "anthropic" => new AnthropicProvider(http, key, settings.Model, settings.Endpoint),
            "google gemini" or "gemini" or "google" => new GeminiProvider(http, key, settings.Model, settings.Endpoint),
            "openai-compatible" or "compatible" or "ollama" => new OpenAiCompatibleProvider(http, key, settings.Model, settings.Endpoint),
            _ => throw new InvalidOperationException($"Unsupported LLM provider '{settings.Provider}'.")
        };
    }
}
