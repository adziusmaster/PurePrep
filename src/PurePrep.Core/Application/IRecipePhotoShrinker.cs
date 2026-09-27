namespace PurePrep.Application;

/// <summary>
/// Downscales and re-encodes a photo before it is stored, so a downloaded page image or a generated one
/// is no larger on disk (and in a backup) than a photo picked in the editor.
/// </summary>
public interface IRecipePhotoShrinker
{
    /// <summary>JPEG bytes at most the stored size, or null when the bytes can't be decoded as an image.</summary>
    Task<byte[]?> ShrinkAsync(byte[] bytes, CancellationToken cancellationToken);
}
