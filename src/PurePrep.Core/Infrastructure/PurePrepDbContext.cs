using Microsoft.EntityFrameworkCore;

namespace PurePrep.Infrastructure;

public sealed class PurePrepDbContext(DbContextOptions<PurePrepDbContext> options) : DbContext(options)
{
    public DbSet<RecipeRecord> Recipes => Set<RecipeRecord>();
}

public sealed class RecipeRecord
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? SourceUrl { get; set; }
    public required string IngredientsJson { get; set; }
    public required string StepsJson { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
