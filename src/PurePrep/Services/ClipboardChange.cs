#if ANDROID
using Android.Content;
#endif

namespace PurePrep.Services;

/// <summary>
/// Tells whether the clipboard holds something new since the last check, without reading its contents.
/// Android 12+ shows a "PurePrep pasted from your clipboard" toast every time an app reads the clip, so
/// the library only reads it when it actually changed — not on every resume.
/// </summary>
internal static class ClipboardChange
{
    private const string LastStampKey = "clipboard_last_stamp";

    /// <summary>
    /// True when the clip changed since the last <see cref="MarkChecked"/> (or can't be told apart, where
    /// the platform has no timestamp — callers then fall back to their own duplicate check).
    /// </summary>
    public static bool ChangedSinceLastCheck()
    {
#if ANDROID
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return true;

        // The description (MIME types + timestamp) can be read without the "pasted" toast; only the
        // clip's contents trigger it.
        var manager = Android.App.Application.Context.GetSystemService(Context.ClipboardService) as ClipboardManager;
        var description = manager?.PrimaryClipDescription;
        if (description is null)
            return false;

        return description.Timestamp != Preferences.Default.Get(LastStampKey, 0L);
#else
        return true;
#endif
    }

    /// <summary>
    /// Records the current clip as seen. Called only after its text was actually read, so a read that
    /// came back empty (e.g. read before the window had focus) is retried on the next check.
    /// </summary>
    public static void MarkChecked()
    {
#if ANDROID
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;
        var manager = Android.App.Application.Context.GetSystemService(Context.ClipboardService) as ClipboardManager;
        if (manager?.PrimaryClipDescription is { } description)
            Preferences.Default.Set(LastStampKey, description.Timestamp);
#endif
    }
}
