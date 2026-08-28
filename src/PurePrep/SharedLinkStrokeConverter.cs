using System.Globalization;

namespace PurePrep;

/// <summary>
/// Picks the import box's border colour: the accent while a shared link sits ready to import, the
/// ordinary line colour otherwise. Resolved from the theme dictionary rather than a literal so it
/// follows the active light/dark palette.
/// </summary>
public sealed class SharedLinkStrokeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is true ? "Lime" : "Line";
        return Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var color) == true
               && color is Color resolved
            ? resolved
            : Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
