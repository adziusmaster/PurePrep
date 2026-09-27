namespace PurePrep.Domain;

public sealed record ParsedRecipe
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

    public int? Servings { get; init; }
    /// <summary>What the servings count counts: "people" (default), or pieces like "pancakes".</summary>
    public string? ServingsNoun { get; init; }
    /// <summary>True when the model estimated servings because the source did not state them.</summary>
    public bool ServingsEstimated { get; init; }
    public int? PrepMinutes { get; init; }
    public int? CookMinutes { get; init; }
    /// <summary>Path of the locally stored photo, relative to the image store root.</summary>
    public string? ImagePath { get; init; }
    /// <summary>The cook's own notes. Never translated, never sent to the server.</summary>
    public string? Notes { get; init; }
    public RecipeStatus Status { get; init; } = RecipeStatus.WantToCook;
    public DateTimeOffset? CookedAt { get; init; }
    public int CookCount { get; init; }
    public bool IsFavourite { get; init; }
    /// <summary>People the cook last scaled this recipe to; null means the original servings.</summary>
    public int? ChosenServings { get; init; }

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

        return this with
        {
            Title = translation.Title,
            Ingredients = translation.Ingredients,
            Steps = translation.Steps,
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
        string? originalLanguage, string? displayLanguage, IReadOnlyDictionary<string, RecipeTranslation> translations) =>
        this with { OriginalLanguage = originalLanguage, DisplayLanguage = displayLanguage, Translations = translations };

    public ParsedRecipe MarkCooked(DateTimeOffset now) =>
        this with { Status = RecipeStatus.Cooked, CookedAt = now, CookCount = CookCount + 1 };

    public ParsedRecipe WithStatus(RecipeStatus status) => this with { Status = status };
    public ParsedRecipe WithFavourite(bool favourite) => this with { IsFavourite = favourite };
    public ParsedRecipe WithNotes(string? notes) =>
        this with { Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim() };
    public ParsedRecipe WithChosenServings(int? servings) =>
        this with { ChosenServings = servings is null ? null : Math.Clamp(servings.Value, 1, 99) };
    public ParsedRecipe WithServings(int? servings) =>
        this with { Servings = servings is null ? null : Math.Clamp(servings.Value, 1, 99), ServingsEstimated = false };
    public ParsedRecipe WithImage(string? imagePath) => this with { ImagePath = imagePath };
}

public sealed record RecipeStep
{
    public int Order { get; init; }
    public required string Instruction { get; init; }
    /// <summary>Named timers from the parser. Empty for legacy recipes (regex fallback applies).</summary>
    public IReadOnlyList<RecipeTimer> Timers { get; init; } = Array.Empty<RecipeTimer>();
    /// <summary>0-based indexes into the recipe's ingredient list used by this step.</summary>
    public IReadOnlyList<int> IngredientRefs { get; init; } = Array.Empty<int>();
}

/// <summary>A single cached translation of a recipe's user-facing text into one language.</summary>
public sealed record RecipeTranslation
{
    public required string Title { get; init; }
    public IReadOnlyList<string> Ingredients { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RecipeStep> Steps { get; init; } = Array.Empty<RecipeStep>();
}

public enum RecipeStatus
{
    WantToCook,
    Cooked,
}
