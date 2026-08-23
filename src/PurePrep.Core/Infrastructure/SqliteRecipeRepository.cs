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
        await db.Database.EnsureCreatedAsync(cancellationToken);
        var records = await db.Recipes.AsNoTracking().ToListAsync(cancellationToken);
        return records.OrderByDescending(x => x.SavedAt).Select(ToDomain).ToArray();
    }

    public async Task SaveAsync(ParsedRecipe recipe, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        db.Recipes.Add(new RecipeRecord
        {
            Id = recipe.Id,
            Title = recipe.Title,
            SourceUrl = recipe.SourceUrl,
            IngredientsJson = JsonSerializer.Serialize(recipe.Ingredients),
            StepsJson = JsonSerializer.Serialize(recipe.Steps),
            SourceSystem = recipe.SourceSystem.ToString(),
            SavedAt = recipe.SavedAt
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ParsedRecipe recipe, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        var record = await db.Recipes.FirstOrDefaultAsync(x => x.Id == recipe.Id, cancellationToken);
        if (record is null)
            return;
        record.Title = recipe.Title;
        record.SourceUrl = recipe.SourceUrl;
        record.IngredientsJson = JsonSerializer.Serialize(recipe.Ingredients);
        record.StepsJson = JsonSerializer.Serialize(recipe.Steps);
        record.SourceSystem = recipe.SourceSystem.ToString();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        var record = await db.Recipes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null)
            return;
        db.Recipes.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ParsedRecipe ToDomain(RecipeRecord record) => new()
    {
        Id = record.Id,
        Title = record.Title,
        SourceUrl = record.SourceUrl,
        Ingredients = JsonSerializer.Deserialize<string[]>(record.IngredientsJson) ?? [],
        Steps = JsonSerializer.Deserialize<RecipeStep[]>(record.StepsJson) ?? [],
        SourceSystem = Enum.TryParse<MeasurementSystem>(record.SourceSystem, out var system) ? system : MeasurementSystem.Metric,
        SavedAt = record.SavedAt
    };
}
