using System.Globalization;

namespace PurePrep.Localization;

/// <summary>
/// Formats a bound value into a localized format string. The ConverterParameter is the
/// resource key of a format string (e.g. "StepsCountFormat" → "{0} steps").
/// </summary>
public sealed class ResourceFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string key || string.IsNullOrEmpty(key))
            return value?.ToString() ?? string.Empty;

        // A whole-number count picks its plural form ("1 step", Polish "3 kroki").
        return value is int count ? AppResources.Plural(key, count) : AppResources.Format(key, value ?? string.Empty);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
