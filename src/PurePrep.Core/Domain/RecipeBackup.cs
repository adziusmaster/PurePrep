using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PurePrep.Domain;

/// <summary>Raised when a file the user picked is not a readable PurePrep backup.</summary>
public sealed class InvalidBackupException(string message) : Exception(message);

/// <summary>
/// Serialises the recipe library so it can leave the device.
///
/// Recipes are stored in a single local SQLite file, so uninstalling or losing the phone loses
/// everything — including imports that cost Smart Credits. A plain, versioned JSON document keeps
/// the library portable and readable by something other than this app.
/// </summary>
public static class RecipeBackup
{
    /// <summary>Bump only for a breaking change; <see cref="Import"/> must keep reading old versions.</summary>
    public const int FormatVersion = 2;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>A restored library plus any embedded photos, keyed by the recipe's <see cref="ParsedRecipe.Id"/>.</summary>
    public sealed record BackupContents(IReadOnlyList<ParsedRecipe> Recipes, IReadOnlyDictionary<Guid, byte[]> Images);

    public static string Export(IEnumerable<ParsedRecipe> recipes, IReadOnlyDictionary<Guid, byte[]>? images = null)
    {
        var document = new BackupDocument(
            FormatVersion,
            DateTimeOffset.UtcNow,
            recipes.Select(r => new BackupRecipe(
                r.Id,
                r.Title,
                r.SourceUrl,
                r.SourceSystem.ToString(),
                r.SavedAt,
                r.Ingredients.ToArray(),
                r.Steps.OrderBy(s => s.Order).Select(s => s.Instruction).ToArray(),
                r.OriginalLanguage,
                r.Translations.Count > 0 ? new Dictionary<string, RecipeTranslation>(r.Translations) : null,
                r.Steps.OrderBy(s => s.Order)
                    .Select(s => new BackupStepDetail(s.Timers.ToArray(), s.IngredientRefs.ToArray()))
                    .ToArray(),
                r.Servings,
                r.ServingsNoun,
                r.ServingsEstimated,
                r.PrepMinutes,
                r.CookMinutes,
                r.Notes,
                r.Status.ToString(),
                r.CookedAt,
                r.CookCount,
                r.IsFavourite,
                r.ChosenServings,
                images?.TryGetValue(r.Id, out var bytes) == true ? Convert.ToBase64String(bytes) : null))
                .ToArray());

        return JsonSerializer.Serialize(document, Options);
    }

    public static IReadOnlyList<ParsedRecipe> Import(string json) => ImportWithImages(json).Recipes;

    public static BackupContents ImportWithImages(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidBackupException("That file is empty.");

        BackupDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<BackupDocument>(json, Options);
        }
        catch (JsonException)
        {
            throw new InvalidBackupException("That file isn't a PurePrep backup.");
        }

        if (document?.Recipes is null || document.Version <= 0)
            throw new InvalidBackupException("That file isn't a PurePrep backup.");

        var recipes = new List<ParsedRecipe>();
        var images = new Dictionary<Guid, byte[]>();
        foreach (var r in document.Recipes)
        {
            // A partially written file should still yield whatever is readable.
            if (string.IsNullOrWhiteSpace(r.Title))
                continue;

            // Resolved once and reused for both the recipe and its image key — otherwise a missing
            // id (Guid.Empty) would mint two different Guid.NewGuid() values and orphan the image.
            var id = r.Id == Guid.Empty ? Guid.NewGuid() : r.Id;
            var recipe = ToRecipe(r, id);
            recipes.Add(recipe);

            if (string.IsNullOrWhiteSpace(r.ImageBase64))
                continue;

            try
            {
                images[id] = Convert.FromBase64String(r.ImageBase64);
            }
            catch (FormatException)
            {
                // A corrupt embedded photo shouldn't sink the whole recipe — skip just the image.
            }
        }

        return new BackupContents(recipes, images);
    }

    private static ParsedRecipe ToRecipe(BackupRecipe r, Guid id)
    {
        var ingredients = (r.Ingredients ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var stepTexts = (r.Steps ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        return new ParsedRecipe
        {
            Id = id,
            Title = r.Title.Trim(),
            SourceUrl = SafeSourceUrl(r.SourceUrl),
            SourceSystem = Enum.TryParse<MeasurementSystem>(r.SourceSystem, out var system)
                ? system
                : MeasurementSystem.Metric,
            SavedAt = r.SavedAt == default ? DateTimeOffset.UtcNow : r.SavedAt,
            Ingredients = ingredients,
            Steps = stepTexts
                .Select((text, index) =>
                {
                    var detail = r.StepDetails is not null && index < r.StepDetails.Length ? r.StepDetails[index] : null;
                    var refs = (detail?.IngredientRefs ?? [])
                        .Where(i => i >= 0 && i < ingredients.Length)
                        .ToArray();
                    return new RecipeStep
                    {
                        Order = index + 1,
                        Instruction = text,
                        Timers = detail?.Timers ?? [],
                        IngredientRefs = refs,
                    };
                })
                .ToArray(),
            // Restore any paid translations so a backup keeps their value; always show the original after
            // a restore (DisplayLanguage is deliberately not carried over).
            OriginalLanguage = string.IsNullOrWhiteSpace(r.OriginalLanguage) ? null : r.OriginalLanguage,
            Translations = r.Translations is { Count: > 0 }
                ? new Dictionary<string, RecipeTranslation>(r.Translations, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, RecipeTranslation>(StringComparer.OrdinalIgnoreCase),
            Servings = r.Servings,
            ServingsNoun = r.ServingsNoun,
            ServingsEstimated = r.ServingsEstimated ?? false,
            PrepMinutes = r.PrepMinutes,
            CookMinutes = r.CookMinutes,
            Notes = r.Notes,
            Status = Enum.TryParse<RecipeStatus>(r.Status, out var status) ? status : RecipeStatus.WantToCook,
            CookedAt = r.CookedAt,
            CookCount = r.CookCount ?? 0,
            IsFavourite = r.IsFavourite ?? false,
            ChosenServings = r.ChosenServings,
            // The embedded photo bytes are restored separately (see ImportWithImages) and saved to a
            // fresh path by the caller — a v1 path on this device would be meaningless anyway.
            ImagePath = null,
        };
    }

    /// <summary>
    /// Keeps a backup's source URL only when it is an http(s) link. A backup file is user-supplied
    /// and may be edited by hand, so a stored URL is untrusted: dropping other schemes stops a
    /// crafted <c>file:</c> or <c>javascript:</c> value ever reaching a link launcher.
    /// </summary>
    private static string? SafeSourceUrl(string? sourceUrl) =>
        !string.IsNullOrWhiteSpace(sourceUrl)
        && Uri.TryCreate(sourceUrl, UriKind.Absolute, out var parsed)
        && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
            ? sourceUrl
            : null;

    /// <summary>Renders one recipe as plain text, for sharing into a message or note.</summary>
    public static string ToPlainText(ParsedRecipe recipe)
    {
        var builder = new StringBuilder();
        builder.AppendLine(recipe.Title).AppendLine();

        if (recipe.Ingredients.Count > 0)
        {
            builder.AppendLine("Ingredients");
            foreach (var ingredient in recipe.Ingredients)
                builder.Append("• ").AppendLine(ingredient);
            builder.AppendLine();
        }

        if (recipe.Steps.Count > 0)
        {
            builder.AppendLine("Method");
            foreach (var step in recipe.Steps.OrderBy(s => s.Order))
                builder.Append(step.Order).Append(". ").AppendLine(step.Instruction);
            builder.AppendLine();
        }

        // Only credit a source when there is one: hand-entered recipes have none, and an empty
        // "Source:" line reads like a bug.
        if (!string.IsNullOrWhiteSpace(recipe.SourceUrl))
            builder.AppendLine(recipe.SourceUrl);

        return builder.ToString().TrimEnd();
    }

    private sealed record BackupDocument(
        int Version,
        DateTimeOffset ExportedAt,
        BackupRecipe[]? Recipes);

    /// <summary>Per-step timers/ingredient refs, parallel to <see cref="BackupRecipe.Steps"/> by index.</summary>
    private sealed record BackupStepDetail(RecipeTimer[]? Timers, int[]? IngredientRefs);

    private sealed record BackupRecipe(
        Guid Id,
        string Title,
        string? SourceUrl,
        string? SourceSystem,
        DateTimeOffset SavedAt,
        string[]? Ingredients,
        string[]? Steps,
        string? OriginalLanguage = null,
        IReadOnlyDictionary<string, RecipeTranslation>? Translations = null,
        BackupStepDetail[]? StepDetails = null,
        int? Servings = null,
        string? ServingsNoun = null,
        bool? ServingsEstimated = null,
        int? PrepMinutes = null,
        int? CookMinutes = null,
        string? Notes = null,
        string? Status = null,
        DateTimeOffset? CookedAt = null,
        int? CookCount = null,
        bool? IsFavourite = null,
        int? ChosenServings = null,
        string? ImageBase64 = null);
}
