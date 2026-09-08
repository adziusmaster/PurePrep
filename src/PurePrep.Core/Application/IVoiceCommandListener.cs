namespace PurePrep.Application;

/// <summary>A hands-free command spoken while cooking in Focus Mode.</summary>
public enum VoiceCommand
{
    Next,
    Previous,
    Repeat,
}

/// <summary>
/// Listens for a small set of spoken navigation commands ("next", "back", "repeat") so a cook with
/// messy hands can move through a recipe without touching the screen.
///
/// Recognition is deliberately best-effort: where the platform has no speech recogniser, or the
/// microphone permission is refused, the app carries on with touch/swipe navigation unchanged.
/// </summary>
public interface IVoiceCommandListener
{
    /// <summary>False where the platform cannot recognise speech (desktop/web preview).</summary>
    bool IsSupported { get; }

    /// <summary>Raised on the UI thread each time a command is recognised.</summary>
    event EventHandler<VoiceCommand>? CommandRecognized;

    /// <summary>
    /// Requests microphone permission if needed and begins listening. Returns whether listening
    /// actually started; callers show the mic as "off" when it did not.
    /// </summary>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops listening and releases the microphone.</summary>
    Task StopAsync();
}

/// <summary>Used where speech recognition is unavailable. Navigation stays touch/swipe only.</summary>
public sealed class UnsupportedVoiceCommandListener : IVoiceCommandListener
{
    public bool IsSupported => false;
    public event EventHandler<VoiceCommand>? CommandRecognized { add { } remove { } }
    public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task StopAsync() => Task.CompletedTask;
}
