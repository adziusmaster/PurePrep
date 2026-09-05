using Android.Content;
using Android.OS;
using Android.Provider;

namespace PurePrep.Platforms.Android;

using PurePrep.Services;

/// <summary>
/// Saves a backup straight into the phone's public <b>Downloads</b> folder via MediaStore. On
/// Android 10+ this needs no runtime permission (scoped storage), and the file shows up in Files /
/// Downloads where people expect a "saved to my phone" export to land.
/// </summary>
public sealed class MediaStoreBackupSaver : ILocalBackupSaver
{
    public bool IsSupported => true;

    public async Task<string?> SaveAsync(string fileName, string contents)
    {
        var context = global::Android.App.Application.Context;
        var resolver = context.ContentResolver;
        if (resolver is null)
            return null;

        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                var values = new ContentValues();
                values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
                values.Put(MediaStore.IMediaColumns.MimeType, "application/json");
                values.Put(MediaStore.IMediaColumns.RelativePath, global::Android.OS.Environment.DirectoryDownloads);

                var collection = MediaStore.Downloads.ExternalContentUri;
                if (collection is null)
                    return null;

                var uri = resolver.Insert(collection, values);
                if (uri is null)
                    return null;

                using (var stream = resolver.OpenOutputStream(uri))
                {
                    if (stream is null)
                        return null;
                    var bytes = System.Text.Encoding.UTF8.GetBytes(contents);
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }

                return global::Android.OS.Environment.DirectoryDownloads;
            }

            // Legacy (pre-scoped-storage) devices: write directly to the shared Downloads directory.
            var downloads = global::Android.OS.Environment
                .GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads);
            if (downloads is null)
                return null;

            var path = System.IO.Path.Combine(downloads.AbsolutePath, fileName);
            await System.IO.File.WriteAllTextAsync(path, contents);
            return global::Android.OS.Environment.DirectoryDownloads;
        }
        catch
        {
            return null;
        }
    }
}
