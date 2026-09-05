using PurePrep.Domain;

namespace PurePrep.Application;

/// <summary>
/// Translates a saved recipe into another language using the backend AI Smart Translator. One Smart
/// Credit is deducted atomically by the server; the result is cached on the device forever, so a
/// language is only ever paid for once. A 402 surfaces as <see cref="InsufficientCreditsException"/>
/// so the UI can show the paywall; other failures surface as <see cref="RecipeImportException"/> with
/// a stable code the UI localizes.
/// </summary>
public interface IRecipeTranslator
{
    Task<RecipeTranslation> TranslateAsync(ParsedRecipe recipe, string targetLanguage, CancellationToken cancellationToken = default);
}
