namespace PurePrep.Services;

/// <summary>Small persisted cooking preferences shared by Settings and Focus Mode.</summary>
public static class CookingSettings
{
    private const string KeepScreenAwakeKey = "keep_screen_awake";

    public static bool KeepScreenAwake
    {
        get => Preferences.Default.Get(KeepScreenAwakeKey, true);
        set => Preferences.Default.Set(KeepScreenAwakeKey, value);
    }
}
