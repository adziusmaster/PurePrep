namespace PurePrep.Domain;

public sealed class ParsedRecipe
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; init; }
    public string? SourceUrl { get; init; }
    public IReadOnlyList<string> Ingredients { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RecipeStep> Steps { get; init; } = Array.Empty<RecipeStep>();

    // Exposed as plain int properties because XAML bindings resolve against the runtime
    // type (an array), whose IReadOnlyCollection<T>.Count is an explicit interface member
    // the binding engine cannot see — binding directly to Steps.Count renders blank.
    public int StepCount => Steps.Count;
    public int IngredientCount => Ingredients.Count;
    public bool HasIngredients => Ingredients.Count > 0;
    public MeasurementSystem SourceSystem { get; init; } = MeasurementSystem.Metric;
    public DateTimeOffset SavedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// ISO 639-1 code of the language the original <see cref="Title"/>/<see cref="Ingredients"/>/
    /// <see cref="Steps"/> are written in, best-effort. <c>null</c> when unknown (e.g. legacy
    /// recipes saved before translation existed, or content our heuristics can't classify).
    /// The original text is <b>always</b> kept pristine so switching back is free and lossless.
    /// </summary>
    public string? OriginalLanguage { get; init; }

    /// <summary>
    /// ISO 639-1 code currently shown on the detail screen. <c>null</c> or empty means the original
    /// is displayed. When set to a key present in <see cref="Translations"/>, that cached translation
    /// is shown instead — no network, no credit.
    /// </summary>
    public string? DisplayLanguage { get; init; }

    /// <summary>
    /// Cached AI translations keyed by ISO 639-1 language code. Each entry is paid for once and then
    /// stored forever, so re-viewing a language never spends another Smart Credit. Never contains the
    /// original language (that lives in the pristine fields above).
    /// </summary>
    public IReadOnlyDictionary<string, RecipeTranslation> Translations { get; init; } =
        new Dictionary<string, RecipeTranslation>(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when a cached translation exists for <paramref name="language"/>.</summary>
    public bool HasTranslation(string? language) =>
        !string.IsNullOrWhiteSpace(language) && Translations.ContainsKey(language);

    /// <summary>
    /// Projects the recipe into the language currently selected in <see cref="DisplayLanguage"/>,
    /// swapping <see cref="Title"/>/<see cref="Ingredients"/>/<see cref="Steps"/> for the cached
    /// translation when one exists. Falls back to the original when the language is the original,
    /// empty, or not cached. Identity fields (Id, SourceUrl, units, timestamps) are preserved so the
    /// result can drive the display pipeline without touching persistence.
    /// </summary>
    public ParsedRecipe Displayed()
    {
        var language = DisplayLanguage;
        if (string.IsNullOrWhiteSpace(language) || !Translations.TryGetValue(language, out var translation))
            return this;

        return new ParsedRecipe
        {
            Id = Id,
            Title = translation.Title,
            SourceUrl = SourceUrl,
            Ingredients = translation.Ingredients,
            Steps = translation.Steps,
            SourceSystem = SourceSystem,
            SavedAt = SavedAt,
            OriginalLanguage = OriginalLanguage,
            DisplayLanguage = DisplayLanguage,
            Translations = Translations,
        };
    }

    /// <summary>Returns a copy with a new cached translation added and that language displayed.</summary>
    public ParsedRecipe WithTranslation(string language, RecipeTranslation translation, string? originalLanguage = null)
    {
        var merged = new Dictionary<string, RecipeTranslation>(Translations, StringComparer.OrdinalIgnoreCase)
        {
            [language] = translation,
        };
        return With(originalLanguage ?? OriginalLanguage, language, merged);
    }

    /// <summary>Returns a copy showing the given language (original when <c>null</c>/empty).</summary>
    public ParsedRecipe WithDisplayLanguage(string? language) =>
        With(OriginalLanguage, language, Translations);

    private ParsedRecipe With(
        string? originalLanguage, string? displayLanguage, IReadOnlyDictionary<string, RecipeTranslation> translations) => new()
    {
        Id = Id,
        Title = Title,
        SourceUrl = SourceUrl,
        Ingredients = Ingredients,
        Steps = Steps,
        SourceSystem = SourceSystem,
        SavedAt = SavedAt,
        OriginalLanguage = originalLanguage,
        DisplayLanguage = displayLanguage,
        Translations = translations,
    };
}

public sealed class RecipeStep
{
    public int Order { get; init; }
    public required string Instruction { get; init; }
}

/// <summary>A single cached translation of a recipe's user-facing text into one language.</summary>
public sealed class RecipeTranslation
{
    public required string Title { get; init; }
    public IReadOnlyList<string> Ingredients { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RecipeStep> Steps { get; init; } = Array.Empty<RecipeStep>();
}