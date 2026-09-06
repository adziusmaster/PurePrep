using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PurePrep.Ai;

/// <summary>Raised when a URL — or any address it redirects to — is not a permitted public http(s) target.</summary>
public sealed class UrlNotAllowedException(string message) : Exception(message);

/// <summary>The site actively refused the request (401/403/429/451/503): a bot wall, rate limit, or legal/geo block.</summary>
public sealed class SiteBlockedException(string message) : Exception(message);

/// <summary>The page does not exist (404/410).</summary>
public sealed class PageNotFoundException(string message) : Exception(message);

/// <summary>A transient upstream failure (network blip, 500/502/504, timeout) that survived the retries.</summary>
public sealed class UpstreamFetchException(string message) : Exception(message);

/// <summary>Fetches the HTML of a user-supplied page, enforcing the SSRF policy on every hop.</summary>
public interface IPageFetcher
{
    Task<string> FetchAsync(Uri url, CancellationToken ct = default);
}

/// <summary>
/// Fetches a page while re-validating <b>every</b> redirect hop through <see cref="IUrlGuard"/>.
///
/// Automatic redirect following is the classic SSRF bypass: the guard approves a public URL, then
/// the handler quietly follows a 302 to a loopback or internal address that was never checked. So
/// the supplied <see cref="HttpClient"/> must be configured with <c>AllowAutoRedirect = false</c>
/// and this class walks the chain itself, guarding each <c>Location</c> before requesting it.
///
/// Identity is <b>honest-first</b>: the first attempt goes out as <c>PurePrepBot</c>. Only if a site
/// walls the bot (a block-class status) do we retry as a browser — and that fallback carries a
/// <c>From</c>/purpose/info header set so a site admin reading their logs sees a transparent,
/// user-initiated recipe import rather than a covert scraper. Hosts that block the bot are
/// remembered so repeat imports skip the wasted first hop.
/// </summary>
public sealed class GuardedPageFetcher : IPageFetcher
{
    private readonly HttpClient _http;
    private readonly IUrlGuard _guard;
    private readonly IFetchHostMemory _hostMemory;
    private readonly PageFetchOptions _options;

    // Two public constructors are visible to the DI container because GuardedPageFetcher is
    // registered as a typed HttpClient. Without this attribute the typed-client factory sees both
    // as applicable (they share the HttpClient/IUrlGuard prefix) and throws at resolution time,
    // 500-ing every import. This attribute names the one constructor DI must use.
    [ActivatorUtilitiesConstructor]
    public GuardedPageFetcher(HttpClient http, IUrlGuard guard, IOptions<PageFetchOptions> options, IFetchHostMemory hostMemory)
        : this(http, guard, options.Value, hostMemory)
    {
    }

    /// <summary>Convenience overload for tests and simple callers that don't wire options/DI.</summary>
    public GuardedPageFetcher(HttpClient http, IUrlGuard guard, PageFetchOptions? options = null, IFetchHostMemory? hostMemory = null)
    {
        _http = http;
        _guard = guard;
        _options = options ?? new PageFetchOptions();
        _hostMemory = hostMemory ?? new InMemoryFetchHostMemory(_options.BlockedHostTtl);
    }

    private enum Identity
    {
        HonestBot,
        TransparentBrowser,
    }

    public async Task<string> FetchAsync(Uri url, CancellationToken ct = default)
    {
        // Skip the honest attempt for hosts we already know wall the bot — it would only waste a hop.
        if (!_hostMemory.IsBlocked(url.Host))
        {
            try
            {
                return await FetchAsAsync(url, Identity.HonestBot, ct);
            }
            catch (SiteBlockedException)
            {
                // The site refused our self-identified bot. Remember it, then retry in the open as a
                // browser carrying our From/purpose headers.
                _hostMemory.MarkBlocked(url.Host);
            }
        }

        return await FetchAsAsync(url, Identity.TransparentBrowser, ct);
    }

    private async Task<string> FetchAsAsync(Uri url, Identity identity, CancellationToken ct)
    {
        var current = url;

        for (var hop = 0; hop <= _options.MaxRedirects; hop++)
        {
            if (!await _guard.IsPublicHttpAsync(current, ct))
                throw new UrlNotAllowedException($"'{current.Host}' is not an allowed public http(s) address.");

            using var response = await SendWithTransientRetryAsync(current, identity, ct);

            if (IsRedirect(response.StatusCode))
            {
                var location = response.Headers.Location
                    ?? throw new UrlNotAllowedException($"'{current.Host}' returned a redirect with no target.");

                // A relative Location is resolved against the hop it came from, exactly as a browser would.
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                continue;
            }

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync(ct);

            throw Classify(response.StatusCode, current.Host);
        }

        throw new UrlNotAllowedException($"'{url.Host}' exceeded {_options.MaxRedirects} redirects.");
    }

    private async Task<HttpResponseMessage> SendWithTransientRetryAsync(Uri url, Identity identity, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(
                    BuildRequest(url, identity), HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (Exception ex) when (attempt < _options.MaxTransientRetries && IsTransientException(ex, ct))
            {
                await Task.Delay(Backoff(attempt), ct);
                continue;
            }

            // A pure "overloaded"/proxy hiccup on the same identity is worth one or two quick retries;
            // block- and not-found-class statuses are handled by the caller (escalate / give up).
            if (attempt < _options.MaxTransientRetries && IsTransientStatus(response.StatusCode))
            {
                response.Dispose();
                await Task.Delay(Backoff(attempt), ct);
                continue;
            }

            return response;
        }
    }

    private HttpRequestMessage BuildRequest(Uri url, Identity identity)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (identity == Identity.HonestBot)
        {
            request.Headers.UserAgent.ParseAdd(_options.BotUserAgent);
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            return request;
        }

        // Browser fallback — look like a browser to get past naive bot walls, but stay transparent:
        // announce who we are, why, and how to reach us so nothing about this is covert.
        request.Headers.UserAgent.ParseAdd(_options.BrowserUserAgent);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        request.Headers.AcceptLanguage.ParseAdd("en;q=0.9,*;q=0.5");

        if (!string.IsNullOrWhiteSpace(_options.ContactEmail) && MailAddressIsValid(_options.ContactEmail))
            request.Headers.From = _options.ContactEmail;
        if (!string.IsNullOrWhiteSpace(_options.PurposeNote))
            request.Headers.TryAddWithoutValidation("X-PurePrep-Purpose", _options.PurposeNote);
        if (!string.IsNullOrWhiteSpace(_options.InfoUrl))
            request.Headers.TryAddWithoutValidation("X-PurePrep-Info", _options.InfoUrl);

        return request;
    }

    private static bool MailAddressIsValid(string value)
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private TimeSpan Backoff(int attempt) => TimeSpan.FromMilliseconds(300 * (attempt + 1));

    private static bool IsRedirect(HttpStatusCode status) => (int)status is >= 300 and < 400;

    // Block-class: the site is refusing us. On the honest attempt this triggers the browser fallback;
    // on the browser attempt it surfaces as SiteBlockedException.
    private static bool IsBlockStatus(HttpStatusCode status) =>
        status is HttpStatusCode.Unauthorized       // 401
            or HttpStatusCode.Forbidden             // 403
            or HttpStatusCode.TooManyRequests       // 429
            or (HttpStatusCode)451                  // Unavailable For Legal Reasons
            or HttpStatusCode.ServiceUnavailable;   // 503 (commonly a challenge page)

    private static bool IsTransientStatus(HttpStatusCode status) =>
        status is HttpStatusCode.InternalServerError   // 500
            or HttpStatusCode.BadGateway               // 502
            or HttpStatusCode.GatewayTimeout           // 504
            or HttpStatusCode.RequestTimeout;          // 408

    private static bool IsTransientException(Exception ex, CancellationToken ct) => ex switch
    {
        // A timeout surfaces as a cancellation whose token is NOT the caller's — retry it.
        OperationCanceledException => !ct.IsCancellationRequested,
        HttpRequestException => true,
        _ => false,
    };

    private static Exception Classify(HttpStatusCode status, string host)
    {
        if (IsBlockStatus(status))
            return new SiteBlockedException($"'{host}' refused the request ({(int)status}).");
        if (status is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            return new PageNotFoundException($"'{host}' has no page there ({(int)status}).");
        return new UpstreamFetchException($"'{host}' returned {(int)status}.");
    }
}
