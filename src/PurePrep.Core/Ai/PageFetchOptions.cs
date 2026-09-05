namespace PurePrep.Ai;

/// <summary>
/// Tunables for <see cref="GuardedPageFetcher"/>. Bound from the <c>PageFetch</c> configuration
/// section. The defaults are deliberately honest-first: we identify ourselves as PurePrepBot and
/// only fall back to a browser identity when a site actively walls the bot — and even then we send
/// transparency headers (see <see cref="ContactEmail"/> / <see cref="InfoUrl"/>) so anyone reading
/// their access log can see exactly who we are and that the fetch was a person pressing "Import".
/// </summary>
public sealed class PageFetchOptions
{
    public const string SectionName = "PageFetch";

    /// <summary>The honest, self-identifying User-Agent used for the first attempt.</summary>
    public string BotUserAgent { get; set; } = "PurePrepBot/1.0 (+https://pureprep.app/bot)";

    /// <summary>Browser User-Agent used only after a site refuses the honest bot.</summary>
    public string BrowserUserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    /// <summary>
    /// Contact email sent as the RFC 7231 <c>From</c> header on the browser fallback, so a site
    /// admin can reach a human. Left blank by default; set it in configuration to enable the header.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>URL of a page explaining the bot, sent as an informational header on the fallback.</summary>
    public string InfoUrl { get; set; } = "https://pureprep.app/bot";

    /// <summary>Plain-language purpose note sent on the browser fallback for anyone reading the logs.</summary>
    public string PurposeNote { get; set; } =
        "User-initiated recipe import from the PurePrep app: a person pasted this link to save a single recipe. " +
        "This is not automated scraping. See the info URL or contact us if this is unwelcome.";

    /// <summary>Redirect hops allowed before the chain is rejected. Real recipe sites need very few.</summary>
    public int MaxRedirects { get; set; } = 5;

    /// <summary>Retries for a genuinely transient failure (network blip, 500/502/504) on one profile.</summary>
    public int MaxTransientRetries { get; set; } = 2;

    /// <summary>How long a host is remembered as "blocks the honest bot" so we skip the wasted first hop.</summary>
    public TimeSpan BlockedHostTtl { get; set; } = TimeSpan.FromHours(6);
}
