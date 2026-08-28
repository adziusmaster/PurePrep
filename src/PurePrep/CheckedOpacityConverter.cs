using System.Globalization;

namespace PurePrep;

/// <summary>Dims a ticked-off ingredient so the ones still to add stay prominent.</summary>
public sealed class CheckedOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 0.45 : 1.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
