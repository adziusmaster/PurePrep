namespace PurePrep.Ai;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>Google AI Studio API key. Supplied via env var Gemini__ApiKey — never committed.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gemini-flash-lite-latest";

    /// <summary>Max characters of page text sent to the model (cost + prompt-injection surface control).</summary>
    public int MaxInputChars { get; set; } = 24000;
}

/// <summary>Structured recipe returned by the AI extractor (before unit normalization).</summary>
public sealed record AiRecipe(string Title, string[] Ingredients, string[] Steps);

public interface IGeminiClient
{
    Task<AiRecipe> ExtractAsync(string pageText, CancellationToken ct = default);
}
