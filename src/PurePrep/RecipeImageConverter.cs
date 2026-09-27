using System.Globalization;
using PurePrep.Application;
using PurePrep.Domain;

namespace PurePrep;

/// <summary>Recipe → its stored photo, or null so the placeholder tile shows.</summary>
public sealed class RecipeImageConverter : IValueConverter
{
    // Resolved once and reused: the DI container doesn't change after startup, and re-resolving on every
    // card render/scroll is wasted work.
    private IRecipeImageStore? _store;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _store ??= IPlatformApplication.Current?.Services.GetService<IRecipeImageStore>();
        var path = _store?.FullPath((value as ParsedRecipe)?.ImagePath);
        if (path is null || !File.Exists(path))
            return null;

        // FromFile(path) would key Android's image cache on the unchanged file path, so a Replace-duplicate
        // re-import (same recipe Id, same images/{id}.jpg path, new bytes) could keep showing the old photo.
        // FromStream reads fresh bytes every time instead.
        return ImageSource.FromStream(() => File.OpenRead(path));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Recipe → "30 min · Serves 4" (parts omitted when unknown).</summary>
public sealed class RecipeCardMetaConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ParsedRecipe r)
            return string.Empty;
        var parts = new List<string>();
        var minutes = (r.PrepMinutes ?? 0) + (r.CookMinutes ?? 0);
        if (minutes > 0)
            parts.Add(Localization.AppResources.Format("MinutesFormat", minutes));
        if (ServingsScale.OriginalServings(r) is { } serves)
            parts.Add(Localization.AppResources.Format("ServesFormat", serves));
        if (parts.Count == 0)
            parts.Add(Localization.AppResources.Plural("StepsCountFormat", r.StepCount));
        return string.Join(" · ", parts);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
