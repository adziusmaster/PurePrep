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
        db.Recipes.Add(new RecipeRecord
        {
            Id = recipe.Id,
            Title = recipe.Title,
            SourceUrl = recipe.SourceUrl,
            IngredientsJson = JsonSerializer.Serialize(recipe.Ingredients),
            StepsJson = JsonSerializer.Serialize(recipe.Steps),
            SourceSystem = recipe.SourceSystem.ToString(),
            SavedAt = recipe.SavedAt,
            OriginalLanguage = recipe.OriginalLanguage,
            DisplayLanguage = recipe.DisplayLanguage,
            TranslationsJson = SerializeTranslations(recipe.Translations)
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ParsedRecipe recipe, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureReadyAsync(db, cancellationToken);
        var record = await db.Recipes.FirstOrDefaultAsync(x => x.Id == recipe.Id, cancellationToken);
        if (record is null)
            return;
        record.Title = recipe.Title;
        record.SourceUrl = recipe.SourceUrl;
        record.IngredientsJson = JsonSerializer.Serialize(recipe.Ingredients);
        record.StepsJson = JsonSerializer.Serialize(recipe.Steps);
        record.SourceSystem = recipe.SourceSystem.ToString();
        record.OriginalLanguage = recipe.OriginalLanguage;
        record.DisplayLanguage = recipe.DisplayLanguage;
        record.TranslationsJson = SerializeTranslations(recipe.Translations);
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
        if (!columns.Contains("OriginalLanguage"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Recipes ADD COLUMN OriginalLanguage TEXT NULL", cancellationToken);
        if (!columns.Contains("DisplayLanguage"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Recipes ADD COLUMN DisplayLanguage TEXT NULL", cancellationToken);
        if (!columns.Contains("TranslationsJson"))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Recipes ADD COLUMN TranslationsJson TEXT NOT NULL DEFAULT '{}'", cancellationToken);
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
        Translations = DeserializeTranslations(record.TranslationsJson)
    };
}
