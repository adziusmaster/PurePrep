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

        return AppResources.Format(key, value ?? string.Empty);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
