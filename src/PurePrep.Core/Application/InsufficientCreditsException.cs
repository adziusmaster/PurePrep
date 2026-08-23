namespace PurePrep.Application;

/// <summary>
/// Thrown when the AI Smart Parser backend rejects an import because the device has no Smart Credits
/// left (HTTP 402). Callers surface the paywall / top-up prompt instead of a generic error.
/// </summary>
public sealed class InsufficientCreditsException : Exception
{
    public InsufficientCreditsException(string? message = null)
        : base(message ?? "You're out of Smart Credits.")
    {
    }
}
