using System.Text.RegularExpressions;

namespace PurePrep.Domain;

/// <summary>
/// Pulls the importable link out of text shared into the app from elsewhere.
///
/// Share sheets almost never send a bare URL: Chrome sends the page title then the link, chat apps
/// wrap it in a sentence, and some clients bracket it. Only http(s) is returned — the URL is handed
/// to the backend fetcher, so other schemes have no business getting that far.
/// </summary>
public static partial class SharedText
{
    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WebUrl();

    /// <summary>The first http(s) URL in <paramref name="sharedText"/>, or <c>null</c> if there is none.</summary>
    public static string? ExtractUrl(string? sharedText)
    {
        if (string.IsNullOrWhiteSpace(sharedText))
            return null;

        var match = WebUrl().Match(sharedText);
        if (!match.Success)
            return null;

        var url = Trim(match.Value);
        return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
               && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
            ? url
            : null;
    }

    /// <summary>
    /// Removes punctuation the surrounding sentence contributed. A trailing slash is kept — it is
    /// part of the path — but a full stop that ended the sentence is not.
    /// </summary>
    private static string Trim(string url)
    {
        var end = url.Length;
        while (end > 0 && IsSentencePunctuation(url[end - 1]))
            end--;

        url = url[..end];

        // Only drop a closing bracket when it has no opener inside the URL, so that links which
        // legitimately contain parentheses (Wikipedia-style) survive intact.
        while (url.Length > 0 && url[^1] is ')' or ']' && Count(url, Opener(url[^1])) < Count(url, url[^1]))
            url = url[..^1];

        return url;

        static bool IsSentencePunctuation(char c) => c is '.' or ',' or '!' or '?' or ';' or ':' or '>' or '"' or '\'';
        static char Opener(char closer) => closer == ')' ? '(' : '[';
        static int Count(string value, char c) => value.Count(x => x == c);
    }
}
