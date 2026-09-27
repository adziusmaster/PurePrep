namespace PurePrep.Ai;

/// <summary>
/// Recognises Google's own hosts (search results, consent pages) so the in-app browser doesn't
/// offer to import them. Matches a <c>google</c> label followed only by a public suffix: one label
/// (<c>google.de</c>) or a two-label country suffix starting with <c>co</c>/<c>com</c>
/// (<c>google.co.uk</c>, <c>google.com.au</c>), with any subdomain in front.
/// </summary>
public static class GoogleHost
{
    public static bool IsGoogle(Uri uri)
    {
        var labels = uri.Host.ToLowerInvariant().Split('.');
        var google = Array.LastIndexOf(labels, "google");
        if (google < 0)
            return false;

        var suffixLabels = labels.Length - google - 1;
        return suffixLabels == 1 || (suffixLabels == 2 && labels[google + 1] is "co" or "com");
    }
}
