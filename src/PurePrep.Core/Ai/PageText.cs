using System.Net;
using HtmlAgilityPack;

namespace PurePrep.Ai;

/// <summary>Reduces raw HTML to readable text, dropping scripts, styles and other non-content nodes.</summary>
public static class PageText
{
    private static readonly string[] Drop = ["script", "style", "noscript", "svg", "nav", "header", "footer", "form", "iframe"];

    public static string Extract(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var tag in Drop)
            foreach (var node in doc.DocumentNode.SelectNodes($"//{tag}") ?? Enumerable.Empty<HtmlNode>())
                node.Remove();

        var raw = doc.DocumentNode.InnerText;
        var decoded = WebUtility.HtmlDecode(raw);
        var lines = decoded
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);
        return string.Join('\n', lines);
    }
}
