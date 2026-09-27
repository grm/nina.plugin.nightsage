namespace NINA.Plugin.NightSage.Providers;

public interface ILLMProvider {
    string Name { get; }
    Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}
