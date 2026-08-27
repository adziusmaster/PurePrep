namespace PurePrep.Ai;

/// <summary>Raised when a URL — or any address it redirects to — is not a permitted public http(s) target.</summary>
public sealed class UrlNotAllowedException(string message) : Exception(message);

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
/// </summary>
public sealed class GuardedPageFetcher(HttpClient http, IUrlGuard guard) : IPageFetcher
{
    /// <summary>Redirect hops allowed before the chain is rejected. Real recipe sites need very few.</summary>
    private const int MaxRedirects = 5;

    public async Task<string> FetchAsync(Uri url, CancellationToken ct = default)
    {
        var current = url;

        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!await guard.IsPublicHttpAsync(current, ct))
                throw new UrlNotAllowedException($"'{Describe(current)}' is not an allowed public http(s) address.");

            using var response = await http.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!IsRedirect(response.StatusCode))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(ct);
            }

            var location = response.Headers.Location
                ?? throw new UrlNotAllowedException($"'{Describe(current)}' returned a redirect with no target.");

            // A relative Location is resolved against the hop it came from, exactly as a browser would.
            current = location.IsAbsoluteUri ? location : new Uri(current, location);
        }

        throw new UrlNotAllowedException($"'{Describe(url)}' exceeded {MaxRedirects} redirects.");
    }

    private static bool IsRedirect(System.Net.HttpStatusCode status) => (int)status is >= 300 and < 400;

    // Never echo the full URL back to the caller: it can carry credentials or tokens in the query.
    private static string Describe(Uri url) => url.Host;
}
