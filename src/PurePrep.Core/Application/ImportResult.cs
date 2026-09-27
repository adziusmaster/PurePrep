using PurePrep.Domain;

namespace PurePrep.Application;

/// <summary>How an import attempt ended, so every entry point can route "out of credits" to Buy credits.</summary>
public enum ImportOutcome
{
    Imported,
    OutOfCredits,
    Cancelled,
    Failed,
}

/// <summary>
/// The result of one import attempt. Out of credits is its own outcome — never folded into a generic
/// failure — so the UI sends the user to Buy Smart Credits instead of a "check your connection" error.
/// </summary>
public sealed record ImportResult(ImportOutcome Outcome, ParsedRecipe? Recipe = null, string? ErrorCode = null)
{
    public static ImportResult Imported(ParsedRecipe recipe) => new(ImportOutcome.Imported, recipe);

    public static ImportResult OutOfCredits { get; } = new(ImportOutcome.OutOfCredits);

    public static ImportResult Cancelled { get; } = new(ImportOutcome.Cancelled);

    public static ImportResult Failed(string code) => new(ImportOutcome.Failed, ErrorCode: code);

    /// <summary>
    /// True only for a known zero balance. An unknown balance (-1, not loaded yet or offline) lets the
    /// attempt through: the backend is the authority and its 402 still maps to <see cref="ImportOutcome.OutOfCredits"/>.
    /// </summary>
    public static bool IsOutOfCredits(int balance) => balance == 0;

    /// <summary>Maps a failed import: 402 → out of credits, a coded backend failure → its code, anything else → unknown.</summary>
    public static ImportResult FromException(Exception error) => error switch
    {
        InsufficientCreditsException => OutOfCredits,
        RecipeImportException coded => Failed(coded.Code),
        _ => Failed(ImportErrorCode.Unknown),
    };
}
