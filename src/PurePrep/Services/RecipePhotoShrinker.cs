using PurePrep.Application;

namespace PurePrep.Services;

/// <summary>
/// Core's <see cref="IRecipePhotoShrinker"/> port backed by <see cref="RecipePhotoResizer"/>: downloaded and
/// AI-generated recipe photos are stored at the same ~1280 px JPEG size as a photo picked in the editor, so
/// a library full of imported recipes doesn't balloon the backup file.
/// </summary>
internal sealed class RecipePhotoShrinker : IRecipePhotoShrinker
{
    public Task<byte[]?> ShrinkAsync(byte[] bytes, CancellationToken cancellationToken) =>
        RecipePhotoResizer.ToJpegAsync(bytes, cancellationToken);
}
