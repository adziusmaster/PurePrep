namespace PurePrep.Domain;

public sealed class ParsedRecipe
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; init; }
    public string? SourceUrl { get; init; }
    public IReadOnlyList<string> Ingredients { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RecipeStep> Steps { get; init; } = Array.Empty<RecipeStep>();
    public MeasurementSystem SourceSystem { get; init; } = MeasurementSystem.Metric;
    public DateTimeOffset SavedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class RecipeStep
{
    public int Order { get; init; }
    public required string Instruction { get; init; }
}