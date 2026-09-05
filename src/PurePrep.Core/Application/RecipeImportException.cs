namespace PurePrep.Application;

/// <summary>
/// Thrown by the recipe parser when the backend returns a handled failure carrying a machine-readable
/// <see cref="Code"/> (one of <see cref="ImportErrorCode"/>). The UI maps the code to a localized
/// message, so the user sees "That site wouldn't let us read the page" instead of a raw HTTP status.
/// </summary>
public sealed class RecipeImportException(string code, string? serverMessage = null)
    : Exception(serverMessage ?? code)
{
    /// <summary>The stable <see cref="ImportErrorCode"/> value the backend returned.</summary>
    public string Code { get; } = code;

    /// <summary>The backend's default (English) message, kept only as a last-resort fallback.</summary>
    public string? ServerMessage { get; } = serverMessage;
}
