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

    // Translation state. The columns above always hold the ORIGINAL text; these hold the detected
    // original language, the language currently displayed, and a JSON cache of paid translations.
    public string? OriginalLanguage { get; set; }
    public string? DisplayLanguage { get; set; }
    public string TranslationsJson { get; set; } = "{}";

    // 1.4 metadata. Nullable or defaulted so upgraded rows load unchanged.
    public int? Servings { get; set; }
    public string? ServingsNoun { get; set; }
    public bool ServingsEstimated { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public string? ImagePath { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = nameof(RecipeStatus.WantToCook);
    public DateTimeOffset? CookedAt { get; set; }
    public int CookCount { get; set; }
    public bool IsFavourite { get; set; }
    public int? ChosenServings { get; set; }
}
