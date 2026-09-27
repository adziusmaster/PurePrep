using PurePrep.Ai;
using PurePrep.Server.Services;

namespace PurePrep.Server.Endpoints;

public static class ImageEndpoint
{
    private const int MaxTitleLength = 200;
    private const int MaxIngredientLength = 120;
    private const int MaxIngredients = 30;

    private static string Cap(string value, int max) => value.Length <= max ? value : value[..max];

    public static async Task<IResult> Generate(
        ImageRequest request, IImageTicketStore tickets, IGeminiClient gemini, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        if (request.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title)
            || !tickets.TryRedeem(request.DeviceId, request.Ticket))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        // The ticket proves an import was paid for, not that the text is sane: bound what reaches the
        // model so a hand-crafted request can't turn one ticket into an oversized prompt.
        var title = Cap(request.Title.Trim(), MaxTitleLength);
        var ingredients = (request.Ingredients ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Take(MaxIngredients)
            .Select(i => Cap(i.Trim(), MaxIngredientLength))
            .ToArray();

        try
        {
            var image = await gemini.GenerateImageAsync(title, ingredients, ct);
            if (!image.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Gemini returned non-image content ({image.MimeType}).");
            return Results.File(image.Bytes, image.MimeType);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            loggerFactory.CreateLogger("ImageEndpoint").LogWarning(ex, "Recipe image generation failed.");
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
