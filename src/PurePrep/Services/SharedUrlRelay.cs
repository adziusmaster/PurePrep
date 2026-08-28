namespace PurePrep.Services;

/// <summary>
/// Carries a URL shared into the app from another app across to the library page.
///
/// The two arrival paths need different handling, which is why this is a relay rather than a direct
/// call: on a cold start the activity receives the intent before any page exists, so the URL has to
/// wait to be collected; on a warm start the page is already on screen and needs to be told.
/// </summary>
public sealed class SharedUrlRelay
{
    private readonly object _gate = new();
    private string? _pending;

    /// <summary>Raised when a URL arrives while the app is already running.</summary>
    public event EventHandler<string>? Received;

    /// <summary>Raised when a share arrived but contained no usable link.</summary>
    public event EventHandler? ReceivedWithoutUrl;

    public void Publish(string url)
    {
        lock (_gate)
            _pending = url;

        Received?.Invoke(this, url);
    }

    public void PublishEmpty() => ReceivedWithoutUrl?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Returns a URL that arrived before the page could listen, clearing it so a share is only
    /// ever applied once.
    /// </summary>
    public string? TakePending()
    {
        lock (_gate)
        {
            var pending = _pending;
            _pending = null;
            return pending;
        }
    }

    /// <summary>Discards anything waiting — used once a share has been applied to the UI.</summary>
    public void Clear()
    {
        lock (_gate)
            _pending = null;
    }
}
