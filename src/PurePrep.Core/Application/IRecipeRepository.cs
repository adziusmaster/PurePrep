using PurePrep.Domain;

namespace PurePrep.Application;

public interface IRecipeRepository
{
    Task<IReadOnlyList<ParsedRecipe>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ParsedRecipe recipe, CancellationToken cancellationToken = default);
    Task UpdateAsync(ParsedRecipe recipe, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
