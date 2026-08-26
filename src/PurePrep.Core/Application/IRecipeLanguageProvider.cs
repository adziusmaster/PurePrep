namespace PurePrep.Application;

/// <summary>
/// Supplies the language that newly parsed recipes should be produced in (an ISO 639-1 code such
/// as "en", "de", "pl"). The AI parser passes this to the backend so recipes land in the user's
/// chosen language regardless of the source site's language. Returns <c>null</c> to keep the
/// recipe in its original language.
/// </summary>
public interface IRecipeLanguageProvider
{
    string? GetRecipeLanguage();
}
