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

    /// <summary>Looks up and formats a localized string.</summary>
    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentUICulture, Get(key), args);
}
