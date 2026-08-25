using PurePrep.Domain;

namespace PurePrep.Services;

/// <summary>How ingredient/step quantities are shown, independent of how the recipe was written.</summary>
public enum UnitDisplay
{
    /// <summary>Show exactly as written in the source recipe (no conversion).</summary>
    Source,
    Metric,
    Imperial
}

/// <summary>Persisted preference for the measurement units shown across the app.</summary>
public static class UnitSettings
{
    private const string Key = "display_units";

    public static UnitDisplay Display
    {
        get => Preferences.Default.Get(Key, nameof(UnitDisplay.Source)) switch
        {
            nameof(UnitDisplay.Metric) => UnitDisplay.Metric,
            nameof(UnitDisplay.Imperial) => UnitDisplay.Imperial,
            _ => UnitDisplay.Source
        };
        set => Preferences.Default.Set(Key, value.ToString());
    }

    /// <summary>The target system for conversion, or <c>null</c> when showing units as written.</summary>
    public static MeasurementSystem? Target => Display switch
    {
        UnitDisplay.Metric => MeasurementSystem.Metric,
        UnitDisplay.Imperial => MeasurementSystem.Imperial,
        _ => null
    };
}
