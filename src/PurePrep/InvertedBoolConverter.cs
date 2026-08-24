using System.Globalization;

namespace PurePrep;

/// <summary>Inverts a bool. Optional string parameter "collapse" is unused; returns the negated bool.</summary>
public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool b ? !b : true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool b ? !b : false;
}
