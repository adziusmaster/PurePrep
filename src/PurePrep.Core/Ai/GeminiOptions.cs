namespace PurePrep.Ai;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>Google AI Studio API key. Supplied via env var Gemini__ApiKey — never committed.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gemini-flash-lite-latest";

    /// <summary>Model used for recipe photo generation (see <see cref="IGeminiClient.GenerateImageAsync"/>).</summary>
    public string ImageModel { get; set; } = "gemini-2.5-flash-image";

    /// <summary>
    /// Max characters of extraction input sent to the model. Raised well above the original 24k:
    /// that cap silently truncated long blog posts, and on those pages the recipe itself could fall
    /// outside the window. The caller budgets within this so structured data is never the part cut.
    /// </summary>
    public int MaxInputChars { get; set; } = 120000;
}

public sealed record AiTimer(string Label, int MinSeconds, int MaxSeconds);

/// <summary>A generated recipe photo — raw bytes plus the MIME type the client needs to render them.</summary>
public sealed record GeneratedImage(byte[] Bytes, string MimeType);

public sealed record AiStep(string Text, int[] IngredientRefs, AiTimer[] Timers);

public sealed record AiRecipeMeta(int? Servings, string? ServingsNoun, bool ServingsEstimated, int? PrepMinutes, int? CookMinutes)
{
    public static AiRecipeMeta Empty { get; } = new(null, null, false, null, null);
}

/// <summary>Structured recipe returned by the AI extractor (before unit normalization).</summary>
public sealed record AiRecipe(string Title, string[] Ingredients, string[] Steps)
{
    public AiRecipeMeta Meta { get; init; } = AiRecipeMeta.Empty;
    /// <summary>Per-step structure, index-aligned with <see cref="Steps"/>.</summary>
    public IReadOnlyList<AiStep> StepDetails { get; init; } = Array.Empty<AiStep>();
    /// <summary>Translation only: timer labels flattened in step order.</summary>
    public IReadOnlyList<string> TimerLabels { get; init; } = Array.Empty<string>();
}

public interface IGeminiClient
{
    Task<AiRecipe> ExtractAsync(string pageText, string? targetLanguage = null, CancellationToken ct = default);

    /// <summary>
    /// Extracts a recipe directly from an image — a photo of a cookbook page, a handwritten card, or
    /// a screenshot of a social post. The model reads the legible text and returns the same clean
    /// Title/Ingredients/Steps structure as <see cref="ExtractAsync"/>. Vision input costs more tokens
    /// than plain text, which is why the caller charges a higher credit price for it.
    /// </summary>
    Task<AiRecipe> ExtractFromImageAsync(byte[] image, string mimeType, string? targetLanguage = null, CancellationToken ct = default);

    /// <summary>
    /// Translates an already-structured recipe into <paramref name="targetLanguage"/> faithfully,
    /// preserving the exact ingredient/step count and every quantity, unit, and number. Unlike
    /// <see cref="ExtractAsync"/> there is no page fetch or extraction — the given content is trusted
    /// structure and only its wording changes.
    /// </summary>
    Task<AiRecipe> TranslateAsync(AiRecipe recipe, string targetLanguage, CancellationToken ct = default);

    /// <summary>
    /// Generates an appetising photo of the finished dish, used when a source page publishes none.
    /// Included in the import credit — see the single-use <c>IImageTicketStore</c> ticket that gates it.
    /// </summary>
    Task<GeneratedImage> GenerateImageAsync(string title, IReadOnlyList<string> ingredients, CancellationToken ct = default);
}
