using PurePrep.Application;
using PurePrep.Localization;

namespace PurePrep.Services;

/// <summary>
/// Persisted preference for the language newly imported recipes should be produced in.
/// An empty stored code means "follow the app language" (the default), so users who never touch
/// this setting still get recipes in the language their UI is in.
/// </summary>
public sealed class RecipeLanguageSettings : IRecipeLanguageProvider
{
    private const string Key = "recipe_language";

    /// <summary>Stored recipe-language code; empty string = follow the app UI language.</summary>
    public static string CurrentCode
    {
        get => Preferences.Get(Key, string.Empty);
        set => Preferences.Set(Key, value ?? string.Empty);
    }

    /// <summary>
    /// The effective recipe language as an ISO 639-1 code. Falls back to the current UI culture when
    /// set to "follow app", and to English when that culture is not one we support.
    /// </summary>
    public string? GetRecipeLanguage()
    {
        var code = CurrentCode;
        if (!string.IsNullOrEmpty(code))
            return code;

        var ui = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var supported = LocalizationService.Supported.Any(l => l.Code == ui);
        return supported ? ui : "en";
    }
}
