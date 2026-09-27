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

    private const string ReadStepsAloudKey = "read_steps_aloud";

    /// <summary>Whether Focus Mode reads each step aloud as you reach it (off by default).</summary>
    public static bool ReadStepsAloud
    {
        get => Preferences.Default.Get(ReadStepsAloudKey, false);
        set => Preferences.Default.Set(ReadStepsAloudKey, value);
    }

    private const string FocusHintSeenKey = "focus_hint_seen";

    /// <summary>Set once Focus Mode has shown its "Tap Next…" hint, which only the very first cook sees.</summary>
    public static bool FocusHintSeen
    {
        get => Preferences.Default.Get(FocusHintSeenKey, false);
        set => Preferences.Default.Set(FocusHintSeenKey, value);
    }
}
