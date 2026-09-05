using PurePrep.Application;

namespace PurePrep.Localization;

/// <summary>
/// Maps a backend <see cref="ImportErrorCode"/> to a localized, user-facing message. This is the
/// client half of the import-error contract: the server sends a stable code, the app turns it into
/// friendly text in the user's language — so a "502" never reaches a tester's screen.
/// </summary>
public static class ImportErrorText
{
    public static string ForCode(string? code) => AppResources.Get(ResourceKeyFor(code));

    private static string ResourceKeyFor(string? code) => code switch
    {
        ImportErrorCode.InvalidUrl => "ImportErrInvalidUrl",
        ImportErrorCode.InvalidRequest => "ImportErrInvalidUrl",
        ImportErrorCode.SiteBlocked => "ImportErrSiteBlocked",
        ImportErrorCode.NotFound => "ImportErrNotFound",
        ImportErrorCode.NoRecipe => "ImportErrNoRecipe",
        ImportErrorCode.Temporary => "ImportErrTemporary",
        ImportErrorCode.ServiceError => "ImportErrService",
        ImportErrorCode.Cancelled => "ImportErrCancelled",
        _ => "ImportErrGeneric",
    };
}
