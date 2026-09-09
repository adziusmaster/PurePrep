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

    private const string PreferredVoiceKey = "read_aloud_voice";

    /// <summary>
    /// Identity (see <c>ReadAloudService.VoiceId</c>) of the cook's chosen reading voice, or empty to
    /// let the app pick a matching one automatically. Only applied when the voice's language matches
    /// the recipe being read, so it never forces an English voice onto a Polish recipe.
    /// </summary>
    public static string PreferredVoiceId
    {
        get => Preferences.Default.Get(PreferredVoiceKey, string.Empty);
        set => Preferences.Default.Set(PreferredVoiceKey, value ?? string.Empty);
    }
}
