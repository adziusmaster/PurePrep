using PurePrep.Domain;

namespace PurePrep.Application;

public interface IRecipeRepository
{
    Task<IReadOnlyList<ParsedRecipe>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ParsedRecipe recipe, CancellationToken cancellationToken = default);
    /// <summary>
    /// Saves every field of an existing recipe <b>except</b> <see cref="ParsedRecipe.ImagePath"/>: the photo
    /// is attached asynchronously after an import, so a caller holding an older copy must never wipe it.
    /// Change the photo with <see cref="UpdateImagePathAsync"/>.
    /// </summary>
    Task UpdateAsync(ParsedRecipe recipe, CancellationToken cancellationToken = default);
    /// <summary>Sets (or, with null, clears) only the stored photo path of an existing recipe.</summary>
    Task UpdateImagePathAsync(Guid id, string? imagePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
