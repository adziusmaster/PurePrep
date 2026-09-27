using Microsoft.Extensions.Logging;
using PurePrep.Application;

namespace PurePrep.Infrastructure;

/// <summary>
/// Stores recipe photos as files under <c>{root}/images/{id}.jpg</c>. Downloaded page images go through
/// the optional <see cref="IRecipePhotoShrinker"/> first, so a 5 MB hero image is stored at editor size.
/// </summary>
public sealed class FileRecipeImageStore(
    HttpClient http,
    string rootDirectory,
    IRecipePhotoShrinker? shrinker = null,
    ILogger<FileRecipeImageStore>? logger = null) : IRecipeImageStore
{
    private const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Sent with every page-image download. HttpClient sends no User-Agent by default, and some recipe
    /// CDNs (RecipeTin Eats' CloudFront) answer a request without one with 403.
    /// </summary>
    public const string UserAgent = "PurePrep (recipe photo; +https://pureprep.app/bot)";

    public async Task<string?> SaveFromUrlAsync(Guid recipeId, Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd("image/*");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!response.IsSuccessStatusCode || mediaType?.StartsWith("image/") != true)
            {
                logger?.LogDebug("Page image from {Host} not used: HTTP {Status}, {MediaType}.", url.Host, (int)response.StatusCode, mediaType ?? "no type");
                return null;
            }
            if (response.Content.Headers.ContentLength > MaxBytes)
            {
                logger?.LogDebug("Page image from {Host} not used: {Length} bytes is over the cap.", url.Host, response.Content.Headers.ContentLength);
                return null;
            }

            // Content-Length is absent for some hosts (chunked transfer), so ReadAsByteArrayAsync alone
            // can't be trusted to bound memory. Stream in chunks and bail out the moment the running
            // total exceeds the cap, instead of buffering an unbounded response first.
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await responseStream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes)
                {
                    logger?.LogDebug("Page image from {Host} not used: body is over the cap.", url.Host);
                    return null;
                }
            }

            byte[]? bytes = buffer.ToArray();
            if (bytes.Length > 0 && shrinker is not null)
                bytes = await shrinker.ShrinkAsync(bytes, cancellationToken);
            if (bytes is not { Length: > 0 })
            {
                logger?.LogDebug("Page image from {Host} not used: empty or could not be decoded.", url.Host);
                return null;
            }
            return await SaveBytesAsync(recipeId, bytes, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Any failure means "no page photo" so the caller can fall back to a generated one. Not just
            // HttpRequestException/IOException: on Android, AndroidMessageHandler reports timeouts, TLS and
            // socket errors as WebException, and a broken body read surfaces as Java.IO.IOException — neither
            // derives from those, and letting them escape skipped the AI fallback altogether.
            logger?.LogDebug("Page image from {Host} not used: {Error}.", url.Host, ex.GetType().Name);
            return null;
        }
    }

    public async Task<string?> SaveBytesAsync(Guid recipeId, byte[] bytes, CancellationToken cancellationToken)
    {
        var relative = $"images/{recipeId}.jpg";
        var full = Path.Combine(rootDirectory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, bytes, cancellationToken);
        return relative;
    }

    public async Task<byte[]?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var full = FullPath(path);
        return full is not null && File.Exists(full) ? await File.ReadAllBytesAsync(full, cancellationToken) : null;
    }

    public Task DeleteAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        var full = Path.Combine(rootDirectory, $"images/{recipeId}.jpg");
        if (File.Exists(full))
            File.Delete(full);
        return Task.CompletedTask;
    }

    public string? FullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            return null;

        var root = Path.GetFullPath(rootDirectory);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, path));

        // Resolve ".."/symlink-style escapes by comparing the fully-resolved candidate against the
        // fully-resolved root, rather than trusting the raw path string (Path.IsPathRooted above only
        // catches an absolute path like "/etc/passwd"; this catches "images/../../x").
        return candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal) ? candidate : null;
    }
}
