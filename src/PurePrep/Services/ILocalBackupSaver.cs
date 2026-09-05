namespace PurePrep.Services;

/// <summary>
/// Saves a backup file to a user-visible location on the device (e.g. the public Downloads folder),
/// as opposed to handing it to the share sheet. Testers asked for a plain "save to my phone" option
/// that doesn't force a route through Drive, email or a chat app.
/// </summary>
public interface ILocalBackupSaver
{
    /// <summary>True when this platform can save directly to a user-visible folder.</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Writes <paramref name="contents"/> under <paramref name="fileName"/> to the platform's public
    /// documents/downloads area. Returns a short, user-friendly location label (e.g. "Downloads") on
    /// success, or <c>null</c> if it could not be saved (caller should fall back to sharing).
    /// </summary>
    Task<string?> SaveAsync(string fileName, string contents);
}

/// <summary>No-op saver for platforms without a public save location; callers fall back to sharing.</summary>
public sealed class UnsupportedLocalBackupSaver : ILocalBackupSaver
{
    public bool IsSupported => false;

    public Task<string?> SaveAsync(string fileName, string contents) => Task.FromResult<string?>(null);
}
