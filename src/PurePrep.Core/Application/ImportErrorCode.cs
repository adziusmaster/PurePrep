namespace PurePrep.Application;

/// <summary>
/// Stable, machine-readable identifiers for the ways a recipe import can fail. The backend returns
/// one of these in the error payload's <c>code</c> field and the client maps it to a localized,
/// user-facing message — so testers never see a raw <c>"502, Bad Gateway"</c> transport string.
/// The strings are part of the client/server contract: keep them in sync, never repurpose one.
/// </summary>
public static class ImportErrorCode
{
    /// <summary>The request itself was malformed (missing/invalid URL or device id).</summary>
    public const string InvalidRequest = "invalid_request";

    /// <summary>The address is not a public recipe page (SSRF policy, non-http(s), private host).</summary>
    public const string InvalidUrl = "invalid_url";

    /// <summary>The device has no Smart Credits left.</summary>
    public const string InsufficientCredits = "insufficient_credits";

    /// <summary>The site actively refused us (401/403/429/451 — bot wall, rate limit, geo/legal block).</summary>
    public const string SiteBlocked = "site_blocked";

    /// <summary>The page does not exist (404/410).</summary>
    public const string NotFound = "not_found";

    /// <summary>The page loaded but no recipe could be extracted from it.</summary>
    public const string NoRecipe = "no_recipe";

    /// <summary>A transient upstream hiccup (site 5xx, slow response, timeout). Retrying may work.</summary>
    public const string Temporary = "temporary";

    /// <summary>Our own extraction service failed (bad key, quota, malformed model response).</summary>
    public const string ServiceError = "service_error";

    /// <summary>The caller cancelled the import.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>Anything not recognised — the client shows a neutral generic message.</summary>
    public const string Unknown = "unknown";
}
