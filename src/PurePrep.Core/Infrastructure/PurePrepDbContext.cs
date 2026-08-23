using Microsoft.EntityFrameworkCore;
using PurePrep.Domain;

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
    public string SourceSystem { get; set; } = nameof(MeasurementSystem.Metric);
    public DateTimeOffset SavedAt { get; set; }
}
