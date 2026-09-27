#if ANDROID
using Android.Graphics;
using AndroidX.ExifInterface.Media;
#else
using Microsoft.Maui.Graphics.Platform;
using IImage = Microsoft.Maui.Graphics.IImage;
#endif

namespace PurePrep.Services;

/// <summary>
/// Shrinks a picked or captured photo before it is stored: at most <see cref="MaxEdge"/> px on the long
/// edge, upright, re-encoded as JPEG. The library card and detail header never show it larger than this.
/// </summary>
/// <remarks>
/// MediaPicker's own MaximumWidth/RotateImage options are deliberately not used: in MAUI 10 both decode
/// the full-resolution bitmap first (a 50 MP camera shot is ~200 MB of pixels), which can crash the app
/// with an out-of-memory error. On Android this decodes a power-of-two subsample instead, so the
/// bitmap in memory is never more than about twice the target size.
/// </remarks>
internal static class RecipePhotoResizer
{
    public const int MaxEdge = 1280;
    private const int JpegQuality = 80;

    /// <summary>The resized JPEG bytes, or null when the stream can't be decoded as an image.</summary>
    /// <remarks>Decoding takes a noticeable moment, so it runs off the UI thread.</remarks>
    public static Task<byte[]?> ToJpegAsync(Stream source, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            try
            {
                using var buffer = new MemoryStream();
                await source.CopyToAsync(buffer, cancellationToken);
                return Resize(buffer.ToArray(), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Unsupported format, corrupt file, out of memory: treat as "no photo".
                return null;
            }
        }, cancellationToken);

    /// <summary>The resized JPEG bytes of an already-buffered image, or null when it can't be decoded.</summary>
    public static Task<byte[]?> ToJpegAsync(byte[] source, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            try
            {
                return Resize(source, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return null;
            }
        }, cancellationToken);

    /// <summary>
    /// The largest power-of-two subsample that still leaves the long edge at least <paramref name="target"/>
    /// px, so the final scale is always a downscale from at most ~2× the target.
    /// </summary>
    internal static int SampleSize(int width, int height, int target)
    {
        var longEdge = Math.Max(width, height);
        var sample = 1;
        while (longEdge / (sample * 2) >= target)
            sample *= 2;
        return sample;
    }

#if ANDROID
    private static byte[]? Resize(byte[] bytes, CancellationToken cancellationToken)
    {
        var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0)
            return null;

        var options = new BitmapFactory.Options { InSampleSize = SampleSize(bounds.OutWidth, bounds.OutHeight, MaxEdge) };
        using var decoded = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, options);
        if (decoded is null)
            return null;

        Bitmap? result = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var matrix = new Matrix();
            var scale = Math.Min(1f, MaxEdge / (float)Math.Max(decoded.Width, decoded.Height));
            if (scale < 1f)
                matrix.PostScale(scale, scale);
            ApplyExifOrientation(matrix, bytes);

            // May return `decoded` itself when the matrix is the identity.
            result = Bitmap.CreateBitmap(decoded, 0, 0, decoded.Width, decoded.Height, matrix, true);
            using var output = new MemoryStream();
            if (!result.Compress(Bitmap.CompressFormat.Jpeg!, JpegQuality, output) || output.Length == 0)
                return null;
            return output.ToArray();
        }
        finally
        {
            // Dispose only drops the managed handle; Recycle frees the native pixels right away. Recycling an
            // already-recycled bitmap is a no-op, so the identity case above is safe.
            if (result is not null && !ReferenceEquals(result, decoded))
            {
                result.Recycle();
                result.Dispose();
            }
            decoded.Recycle();
        }
    }

    // BitmapFactory ignores the EXIF orientation tag; camera shots are often stored sideways with the tag
    // saying how to turn them. The re-encoded JPEG carries no EXIF, so the rotation is baked into the pixels.
    private static void ApplyExifOrientation(Matrix matrix, byte[] bytes)
    {
        int orientation;
        try
        {
            using var exifStream = new MemoryStream(bytes);
            orientation = new ExifInterface(exifStream).GetAttributeInt(ExifInterface.TagOrientation, ExifInterface.OrientationNormal);
        }
        catch (Exception)
        {
            return;
        }

        switch (orientation)
        {
            case ExifInterface.OrientationFlipHorizontal: matrix.PostScale(-1, 1); break;
            case ExifInterface.OrientationRotate180: matrix.PostRotate(180); break;
            case ExifInterface.OrientationFlipVertical: matrix.PostScale(1, -1); break;
            case ExifInterface.OrientationTranspose: matrix.PostRotate(90); matrix.PostScale(-1, 1); break;
            case ExifInterface.OrientationRotate90: matrix.PostRotate(90); break;
            case ExifInterface.OrientationTransverse: matrix.PostRotate(270); matrix.PostScale(-1, 1); break;
            case ExifInterface.OrientationRotate270: matrix.PostRotate(270); break;
        }
    }
#else
    private static byte[]? Resize(byte[] bytes, CancellationToken cancellationToken)
    {
        using var input = new MemoryStream(bytes);
        IImage? image = PlatformImage.FromStream(input);
        if (image is null || image.Width <= 0 || image.Height <= 0)
            return null;

        if (Math.Max(image.Width, image.Height) > MaxEdge)
            image = image.Downsize(MaxEdge, disposeOriginal: true);

        using (image)
        using (var output = new MemoryStream())
        {
            cancellationToken.ThrowIfCancellationRequested();
            image.Save(output, ImageFormat.Jpeg, JpegQuality / 100f);
            return output.Length > 0 ? output.ToArray() : null;
        }
    }
#endif
}
