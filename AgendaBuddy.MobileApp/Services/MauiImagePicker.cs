#if MOBILE
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace AgendaBuddy.MobileApp.Services;

/// <inheritdoc cref="IImagePicker"/>
/// <remarks>
/// The system picker needs no photo-library permission on either platform, which is why none is requested.
/// Every picked file is decoded and re-encoded here rather than trusting the picker's own resize options: the
/// server only accepts JPEG, PNG and WebP, and an iPhone's HEIC original would otherwise arrive as HEIC.
/// </remarks>
public sealed class MauiImagePicker : IImagePicker
{
    private const float JpegQuality = 0.85f;

    public async Task<IReadOnlyList<byte[]>> PickAsync(int limit, CancellationToken ct = default)
    {
        if (limit < 1)
            return Array.Empty<byte[]>();

        var files = await MainThread.InvokeOnMainThreadAsync(() => PickFilesAsync(limit));
        var result = new List<byte[]>();
        foreach (var file in files.Take(limit))
        {
            ct.ThrowIfCancellationRequested();
            var jpeg = await EncodeAsync(file);
            if (jpeg is not null)
                result.Add(jpeg);
        }

        return result;
    }

    private static async Task<IReadOnlyList<FileResult>> PickFilesAsync(int limit)
    {
        var options = new MediaPickerOptions
        {
            SelectionLimit = limit,
            MaximumWidth = IImagePicker.MaxLongEdge,
            MaximumHeight = IImagePicker.MaxLongEdge
        };
        var picked = await MediaPicker.Default.PickPhotosAsync(options);
        return picked?.Where(f => f is not null).ToList() ?? new List<FileResult>();
    }

    private static async Task<byte[]?> EncodeAsync(FileResult file)
    {
        await using var stream = await file.OpenReadAsync();
        var image = PlatformImage.FromStream(stream);
        if (image is null)
            return null;

        var longEdge = Math.Max(image.Width, image.Height);
        if (longEdge > IImagePicker.MaxLongEdge)
            image = image.Downsize(IImagePicker.MaxLongEdge, disposeOriginal: true);

        using (image)
        {
            return await image.AsBytesAsync(ImageFormat.Jpeg, JpegQuality);
        }
    }
}
#endif
