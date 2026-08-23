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
        var records = await db.Recipes.AsNoTracking().OrderByDescending(x => x.SavedAt).ToListAsync(cancellationToken);
        return records.Select(ToDomain).ToArray();
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
            SavedAt = recipe.SavedAt
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ParsedRecipe ToDomain(RecipeRecord record) => new()
    {
        Id = record.Id,
        Title = record.Title,
        SourceUrl = record.SourceUrl,
        Ingredients = JsonSerializer.Deserialize<string[]>(record.IngredientsJson) ?? [],
        Steps = JsonSerializer.Deserialize<RecipeStep[]>(record.StepsJson) ?? [],
        SavedAt = record.SavedAt
    };
}
