using PurePrep.Domain;

namespace PurePrep.Application;

public interface IRecipeParser
{
    Task<ParsedRecipe> ParseAsync(Uri source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts a recipe from a photo or screenshot (a cookbook page, a handwritten card, a social
    /// post). Costs more credits than a URL/text import because vision input is more token-hungry.
    /// </summary>
    Task<ParsedRecipe> ParseImageAsync(byte[] image, string mimeType, CancellationToken cancellationToken = default);

    /// <summary>Extracts a recipe from pasted free text (notes, a message, a PDF's contents).</summary>
    Task<ParsedRecipe> ParseTextAsync(string text, CancellationToken cancellationToken = default);
}
