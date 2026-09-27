namespace PurePrep.Application;

public interface IRecipeImageStore
{
    Task<string?> SaveFromUrlAsync(Guid recipeId, Uri url, CancellationToken cancellationToken);
    Task<string?> SaveBytesAsync(Guid recipeId, byte[] bytes, CancellationToken cancellationToken);
    Task<byte[]?> ReadAsync(string path, CancellationToken cancellationToken);
    Task DeleteAsync(Guid recipeId, CancellationToken cancellationToken);
    /// <summary>Absolute file path for binding an Image control; null when no image.</summary>
    string? FullPath(string? path);
}
