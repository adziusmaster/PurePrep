using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PurePrep.Application;

namespace PurePrep.Infrastructure;

public sealed class AiProxyRecipeImageGenerator(
    HttpClient http,
    IDeviceIdentity identity,
    ILogger<AiProxyRecipeImageGenerator>? logger = null) : IRecipeImageGenerator
{
    public async Task<byte[]?> GenerateAsync(string ticket, string title, IReadOnlyList<string> ingredients, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deviceId = await identity.GetDeviceIdAsync(cancellationToken);
            using var response = await http.PostAsJsonAsync("api/ai/image", new { deviceId, ticket, title, ingredients }, cancellationToken);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            logger?.LogDebug("Generated photo not available: HTTP {Status}.", (int)response.StatusCode);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Any failure keeps the placeholder. On Android, socket/TLS/timeout failures arrive as
            // WebException or Java exceptions rather than HttpRequestException, so catch them all.
            logger?.LogDebug("Generated photo not available: {Error}.", ex.GetType().Name);
            return null;
        }
    }
}
