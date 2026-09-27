using Microsoft.Extensions.Logging;
using PurePrep.Domain;

namespace PurePrep.Application;

/// <summary>
/// Gives a just-imported recipe its photo: the page's own image when it downloads, otherwise an
/// AI-generated one (already paid for by the import). Any failure leaves the placeholder. Only the photo
/// path is written, so edits made to the recipe while the photo was on its way are never overwritten.
/// </summary>
public sealed class RecipeImageAttacher(
    IRecipeImageStore store,
    IRecipeImageGenerator generator,
    IRecipeRepository repository,
    IRecipePhotoShrinker? shrinker = null,
    ILogger<RecipeImageAttacher>? logger = null)
{
    public async Task<ParsedRecipe> AttachAsync(ParsedImport import, ParsedRecipe saved, CancellationToken cancellationToken)
    {
        string? path = null;
        if (import.ImageUrl is not null)
        {
            try
            {
                path = await store.SaveFromUrlAsync(saved.Id, import.ImageUrl, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // A failed page photo must still fall through to the generated one below.
                logger?.LogDebug("Page photo download failed: {Error}.", ex.GetType().Name);
            }
        }

        // The server always issues a ticket; it is only redeemed when there was no page image or it
        // failed to download, so a working page photo never costs a generation.
        if (path is null && !string.IsNullOrWhiteSpace(import.ImageTicket))
        {
            try
            {
                var bytes = await generator.GenerateAsync(import.ImageTicket, saved.Title, saved.Ingredients, cancellationToken);
                if (bytes is { Length: > 0 } && shrinker is not null)
                    bytes = await shrinker.ShrinkAsync(bytes, cancellationToken);
                if (bytes is { Length: > 0 })
                    path = await store.SaveBytesAsync(saved.Id, bytes, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger?.LogDebug("Generated photo failed: {Error}.", ex.GetType().Name);
            }
        }

        if (path is null)
        {
            logger?.LogDebug(
                "Recipe kept its placeholder (page image {PageImage}, ticket {Ticket}).",
                import.ImageUrl is null ? "absent" : "failed",
                string.IsNullOrWhiteSpace(import.ImageTicket) ? "absent" : "present");
            return saved;
        }

        await repository.UpdateImagePathAsync(saved.Id, path, cancellationToken);
        return saved.WithImage(path);
    }
}
