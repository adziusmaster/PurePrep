namespace PurePrep.Ai;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>Google AI Studio API key. Supplied via env var Gemini__ApiKey — never committed.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gemini-flash-lite-latest";

    /// <summary>
    /// Max characters of extraction input sent to the model. Raised well above the original 24k:
    /// that cap silently truncated long blog posts, and on those pages the recipe itself could fall
    /// outside the window. The caller budgets within this so structured data is never the part cut.
    /// </summary>
    public int MaxInputChars { get; set; } = 120000;
}

/// <summary>Structured recipe returned by the AI extractor (before unit normalization).</summary>
public sealed record AiRecipe(string Title, string[] Ingredients, string[] Steps);

public interface IGeminiClient
{
    Task<AiRecipe> ExtractAsync(string pageText, string? targetLanguage = null, CancellationToken ct = default);
}
