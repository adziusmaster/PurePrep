using System.Globalization;
using PurePrep.Domain;
using PurePrep.Localization;

namespace PurePrep;

/// <summary>
/// A <see cref="RecipeTimer"/> → its chip text, e.g. "Fry onion · 10–12 min". Mirrors
/// <see cref="RecipeTimer.DurationText"/>'s formatting exactly, but through the app's resx tables
/// instead of Core's hardcoded English "min"/"s"/"h" — Core stays UI-copy-free (and its own tests
/// keep exercising <see cref="RecipeTimer.DurationText"/> directly), while the chip on screen reads
/// in the recipe/app's own language.
/// </summary>
public sealed class TimerChipConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is RecipeTimer timer ? $"{timer.Label} · {FormatDuration(timer)}" : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    private static string FormatDuration(RecipeTimer timer) =>
        timer.IsRange && timer.MinSeconds % 60 == 0 && timer.MaxSeconds % 60 == 0 && timer.MaxSeconds < 3600
            ? AppResources.Format("DurationRangeMinutesFormat", timer.MinSeconds / 60, timer.MaxSeconds / 60)
            : timer.IsRange
                ? $"{Format(timer.MinSeconds)}–{Format(timer.MaxSeconds)}"
                : Format(timer.MinSeconds);

    private static string Format(int seconds)
    {
        if (seconds < 60)
            return AppResources.Format("DurationSecondsFormat", seconds);

        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours < 1)
        {
            return span.Seconds == 0
                ? AppResources.Format("DurationMinutesFormat", (int)span.TotalMinutes)
                : AppResources.Format("DurationMinutesSecondsFormat", (int)span.TotalMinutes, span.Seconds);
        }

        return span.Minutes == 0
            ? AppResources.Format("DurationHoursFormat", (int)span.TotalHours)
            : AppResources.Format("DurationHoursMinutesFormat", (int)span.TotalHours, span.Minutes);
    }
}
