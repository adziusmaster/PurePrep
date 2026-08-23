using PurePrep.Domain;

namespace PurePrep.Application;

public interface IRecipeParser
{
    Task<ParsedRecipe> ParseAsync(Uri source, CancellationToken cancellationToken = default);
}
