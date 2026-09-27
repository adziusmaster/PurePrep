using System.Globalization;

namespace PurePrep;

/// <summary>A recipe title → its capitalized first letter, for the photo placeholder tile.</summary>
public sealed class FirstLetterConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && s.Length > 0 ? char.ToUpperInvariant(s[0]).ToString() : "?";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
