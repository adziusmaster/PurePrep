using System.Globalization;
using System.Reflection;
using System.Resources;

namespace PurePrep.Localization;

/// <summary>
/// Strongly-typed-ish accessor over the embedded AppResources.*.resx string tables.
/// The ResourceManager base name is discovered from the assembly manifest at startup so
/// it stays correct regardless of the project's root namespace / folder layout.
/// </summary>
public static class AppResources
{
    private static readonly ResourceManager Manager = CreateManager();

    private static ResourceManager CreateManager()
    {
        var assembly = typeof(AppResources).Assembly;
        // Neutral resource is compiled as "<something>.AppResources.resources".
        var manifest = Array.Find(assembly.GetManifestResourceNames(),
            n => n.EndsWith("AppResources.resources", StringComparison.Ordinal));
        var baseName = manifest is null
            ? "PurePrep.Resources.Localization.AppResources"
            : manifest[..^".resources".Length];
        return new ResourceManager(baseName, assembly);
    }

    /// <summary>Looks up a localized string for the current UI culture, falling back to the key.</summary>
    public static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>Looks up a localized string for an explicit culture, falling back to the key.</summary>
    public static string Get(string key, CultureInfo culture) =>
        Manager.GetString(key, culture) ?? key;

    /// <summary>Looks up and formats a localized string.</summary>
    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentUICulture, Get(key), args);

    /// <summary>
    /// Formats a count with the plural form the UI language needs: <c>{key}One</c> for "1 step",
    /// <c>{key}Few</c> for Polish "2–4 kroki", else <paramref name="key"/> itself. A key without
    /// variants in the resx files simply uses <paramref name="key"/> for every count.
    /// </summary>
    public static string Plural(string key, int count)
    {
        var culture = CultureInfo.CurrentUICulture;
        var suffix = Domain.PluralRules.For(culture.TwoLetterISOLanguageName, count) switch
        {
            Domain.PluralCategory.One => "One",
            Domain.PluralCategory.Few => "Few",
            _ => null,
        };
        var format = (suffix is null ? null : Manager.GetString(key + suffix, culture)) ?? Get(key);
        return string.Format(culture, format, count);
    }
}
