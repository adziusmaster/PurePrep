namespace PurePrep.Application;

public interface IRecipeImageGenerator
{
    /// <summary>Returns image bytes, or null when generation failed (never throws for service errors).</summary>
    Task<byte[]?> GenerateAsync(string ticket, string title, IReadOnlyList<string> ingredients, CancellationToken cancellationToken);
}
