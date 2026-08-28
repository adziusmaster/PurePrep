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
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Export(IEnumerable<ParsedRecipe> recipes)
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
                r.Steps.OrderBy(s => s.Order).Select(s => s.Instruction).ToArray())).ToArray());

        return JsonSerializer.Serialize(document, Options);
    }

    public static IReadOnlyList<ParsedRecipe> Import(string json)
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

        return document.Recipes
            // A partially written file should still yield whatever is readable.
            .Where(r => !string.IsNullOrWhiteSpace(r.Title))
            .Select(ToRecipe)
            .ToArray();
    }

    private static ParsedRecipe ToRecipe(BackupRecipe r) => new()
    {
        Id = r.Id == Guid.Empty ? Guid.NewGuid() : r.Id,
        Title = r.Title.Trim(),
        SourceUrl = r.SourceUrl,
        SourceSystem = Enum.TryParse<MeasurementSystem>(r.SourceSystem, out var system)
            ? system
            : MeasurementSystem.Metric,
        SavedAt = r.SavedAt == default ? DateTimeOffset.UtcNow : r.SavedAt,
        Ingredients = (r.Ingredients ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(),
        Steps = (r.Steps ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select((text, index) => new RecipeStep { Order = index + 1, Instruction = text })
            .ToArray(),
    };

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

    private sealed record BackupRecipe(
        Guid Id,
        string Title,
        string? SourceUrl,
        string? SourceSystem,
        DateTimeOffset SavedAt,
        string[]? Ingredients,
        string[]? Steps);
}
