using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PurePrep.Application;
using PurePrep.Domain;

namespace PurePrep.Infrastructure;

public sealed class SqliteRecipeRepository(IDbContextFactory<PurePrepDbContext> contextFactory) : IRecipeRepository
{
    public async Task<IReadOnlyList<ParsedRecipe>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureReadyAsync(db, cancellationToken);
        var records = await db.Recipes.AsNoTracking().ToListAsync(cancellationToken);
        return records.OrderByDescending(x => x.SavedAt).Select(ToDomain).ToArray();
    }

    public async Task SaveAsync(ParsedRecipe recipe, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureReadyAsync(db, cancellationToken);
        var record = new RecipeRecord
        {
            Id = recipe.Id,
            Title = recipe.Title,
            IngredientsJson = "",
            StepsJson = "",
            SavedAt = recipe.SavedAt
        };
        Fill(record, recipe);
        record.ImagePath = recipe.ImagePath;
        db.Recipes.Add(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ParsedRecipe recipe, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureReadyAsync(db, cancellationToken);
        var record = await db.Recipes.FirstOrDefaultAsync(x => x.Id == recipe.Id, cancellationToken);
        if (record is null)
            return;
        // ImagePath is deliberately left alone (see IRecipeRepository.UpdateAsync).
        Fill(record, recipe);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateImagePathAsync(Guid id, string? imagePath, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureReadyAsync(db, cancellationToken);
        var record = await db.Recipes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null)
            return;
        record.ImagePath = imagePath;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureReadyAsync(db, cancellationToken);
        var record = await db.Recipes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null)
            return;
        db.Recipes.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureReadyAsync(PurePrepDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        var columns = await db.Database
            .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Recipes')")
            .ToListAsync(cancellationToken);
        if (!columns.Contains("SourceSystem"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Recipes ADD COLUMN SourceSystem TEXT NOT NULL DEFAULT 'Metric'",
                cancellationToken);
        }

        // Additive translation columns. Existing rows get NULL languages and an empty cache, so their
        // current text is treated as the pristine original — no data loss, no reprocessing needed.
        // NOTE: the default is an empty string, NOT '{}'. ExecuteSqlRaw runs the SQL through
        // string.Format, which reads a literal "{}" as a malformed placeholder and throws
        // FormatException (crashing on the first upgraded-DB launch). An empty string is treated as
        // an empty map by DeserializeTranslations, so it's equivalent and brace-free.
        if (!columns.Contains("OriginalLanguage"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Recipes ADD COLUMN OriginalLanguage TEXT NULL", cancellationToken);
        if (!columns.Contains("DisplayLanguage"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Recipes ADD COLUMN DisplayLanguage TEXT NULL", cancellationToken);
        if (!columns.Contains("TranslationsJson"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Recipes ADD COLUMN TranslationsJson TEXT NOT NULL DEFAULT ''", cancellationToken);

        // 1.4 columns — additive, brace-free SQL (ExecuteSqlRaw formats the string).
        (string Name, string Ddl)[] added =
        [
            ("Servings", "INTEGER NULL"),
            ("ServingsNoun", "TEXT NULL"),
            ("ServingsEstimated", "INTEGER NOT NULL DEFAULT 0"),
            ("PrepMinutes", "INTEGER NULL"),
            ("CookMinutes", "INTEGER NULL"),
            ("ImagePath", "TEXT NULL"),
            ("Notes", "TEXT NULL"),
            ("Status", "TEXT NOT NULL DEFAULT 'WantToCook'"),
            ("CookedAt", "TEXT NULL"),
            ("CookCount", "INTEGER NOT NULL DEFAULT 0"),
            ("IsFavourite", "INTEGER NOT NULL DEFAULT 0"),
            ("ChosenServings", "INTEGER NULL"),
        ];
        foreach (var (name, ddl) in added)
        {
            if (!columns.Contains(name))
                await db.Database.ExecuteSqlRawAsync($"ALTER TABLE Recipes ADD COLUMN {name} {ddl}", cancellationToken);
        }
    }

    private static void Fill(RecipeRecord record, ParsedRecipe recipe)
    {
        record.Title = recipe.Title;
        record.SourceUrl = recipe.SourceUrl;
        record.IngredientsJson = JsonSerializer.Serialize(recipe.Ingredients);
        record.StepsJson = JsonSerializer.Serialize(recipe.Steps);
        record.SourceSystem = recipe.SourceSystem.ToString();
        record.OriginalLanguage = recipe.OriginalLanguage;
        record.DisplayLanguage = recipe.DisplayLanguage;
        record.TranslationsJson = SerializeTranslations(recipe.Translations);
        record.Servings = recipe.Servings;
        record.ServingsNoun = recipe.ServingsNoun;
        record.ServingsEstimated = recipe.ServingsEstimated;
        record.PrepMinutes = recipe.PrepMinutes;
        record.CookMinutes = recipe.CookMinutes;
        record.Notes = recipe.Notes;
        record.Status = recipe.Status.ToString();
        record.CookedAt = recipe.CookedAt;
        record.CookCount = recipe.CookCount;
        record.IsFavourite = recipe.IsFavourite;
        record.ChosenServings = recipe.ChosenServings;
    }

    private static string SerializeTranslations(IReadOnlyDictionary<string, RecipeTranslation> translations) =>
        JsonSerializer.Serialize(translations);

    private static IReadOnlyDictionary<string, RecipeTranslation> DeserializeTranslations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, RecipeTranslation>(StringComparer.OrdinalIgnoreCase);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, RecipeTranslation>>(json);
        return parsed is null
            ? new Dictionary<string, RecipeTranslation>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, RecipeTranslation>(parsed, StringComparer.OrdinalIgnoreCase);
    }

    private static ParsedRecipe ToDomain(RecipeRecord record) => new()
    {
        Id = record.Id,
        Title = record.Title,
        SourceUrl = record.SourceUrl,
        Ingredients = JsonSerializer.Deserialize<string[]>(record.IngredientsJson) ?? [],
        Steps = JsonSerializer.Deserialize<RecipeStep[]>(record.StepsJson) ?? [],
        SourceSystem = Enum.TryParse<MeasurementSystem>(record.SourceSystem, out var system) ? system : MeasurementSystem.Metric,
        SavedAt = record.SavedAt,
        OriginalLanguage = record.OriginalLanguage,
        DisplayLanguage = record.DisplayLanguage,
        Translations = DeserializeTranslations(record.TranslationsJson),
        Servings = record.Servings,
        ServingsNoun = record.ServingsNoun,
        ServingsEstimated = record.ServingsEstimated,
        PrepMinutes = record.PrepMinutes,
        CookMinutes = record.CookMinutes,
        ImagePath = record.ImagePath,
        Notes = record.Notes,
        Status = Enum.TryParse<RecipeStatus>(record.Status, out var status) ? status : RecipeStatus.WantToCook,
        CookedAt = record.CookedAt,
        CookCount = record.CookCount,
        IsFavourite = record.IsFavourite,
        ChosenServings = record.ChosenServings,
    };
}
