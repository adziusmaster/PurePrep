using System.Globalization;
using Microsoft.Maui.Storage;

namespace PurePrep.Localization;

/// <summary>The languages PurePrep ships UI translations for.</summary>
public sealed record AppLanguage(string Code, string NativeName);

/// <summary>
/// Applies and persists the app UI language. "System" (empty stored code) follows the OS.
/// Localized XAML strings read <see cref="CultureInfo.CurrentUICulture"/> at load time, so a
/// language change takes effect by rebuilding the root page (see App.ApplyLanguageAndReload).
/// </summary>
public static class LocalizationService
{
    private const string PreferenceKey = "app_language";

    /// <summary>Supported UI languages, in display order. Empty code = follow system.</summary>
    public static IReadOnlyList<AppLanguage> Supported { get; } = new[]
    {
        new AppLanguage("", string.Empty), // System default (label resolved in UI)
        new AppLanguage("en", "English"),
        new AppLanguage("de", "Deutsch"),
        new AppLanguage("fr", "Français"),
        new AppLanguage("es", "Español"),
        new AppLanguage("it", "Italiano"),
        new AppLanguage("pl", "Polski"),
        new AppLanguage("nl", "Nederlands"),
    };

    /// <summary>The stored language code ("" means follow system).</summary>
    public static string CurrentCode
    {
        get => Preferences.Get(PreferenceKey, string.Empty);
        private set => Preferences.Set(PreferenceKey, value);
    }

    /// <summary>Applies the stored (or given) language to the current + default thread cultures.</summary>
    public static void Apply(string? code = null)
    {
        code ??= CurrentCode;

        CultureInfo culture;
        if (string.IsNullOrEmpty(code))
        {
            // Follow the OS, but only if it is one we translate; otherwise fall back to English.
            var os = TryGetOsCulture();
            culture = IsSupportedCode(os?.TwoLetterISOLanguageName) ? os! : new CultureInfo("en");
        }
        else
        {
            culture = new CultureInfo(code);
        }

        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
    }

    /// <summary>Persists the selected language code and applies it.</summary>
    public static void Set(string code)
    {
        CurrentCode = code;
        Apply(code);
    }

    private static bool IsSupportedCode(string? twoLetter) =>
        !string.IsNullOrEmpty(twoLetter) &&
        Supported.Any(l => l.Code == twoLetter);

    private static CultureInfo? TryGetOsCulture()
    {
        try { return CultureInfo.InstalledUICulture; }
        catch { return null; }
    }
}
