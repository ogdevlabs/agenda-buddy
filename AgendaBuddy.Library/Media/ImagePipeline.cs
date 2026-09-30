using System.Security.Cryptography;
using SkiaSharp;

namespace AgendaBuddy.Library.Media;

public static class ImageRejections
{
    public const string UnsupportedFormat = "unsupported-format";
    public const string TooLarge = "too-large";
    public const string TooManyPixels = "too-many-pixels";
    public const string DimensionExceeded = "dimension-exceeded";
    public const string Undecodable = "undecodable";

    public static string Describe(string code) => code switch
    {
        UnsupportedFormat => "Use a JPEG, PNG or WebP photo.",
        TooLarge => "This photo is larger than 5 MB.",
        TooManyPixels => "This photo has more than 40 megapixels.",
        DimensionExceeded => "This photo is more than 8000 pixels on a side.",
        _ => "This file couldn't be opened as a photo."
    };
}

public sealed record ProcessedImage(byte[] Full, int Width, int Height, byte[] Thumb, string Hash);

public sealed record ImagePipelineResult(ProcessedImage? Image, string? Rejection)
{
    public static ImagePipelineResult Rejected(string code) => new(null, code);
}

/// <summary>
/// Turns uploaded bytes into the only bytes ever served back: a re-encoded sRGB JPEG and its thumbnail.
/// </summary>
/// <remarks>
/// Nothing a client sent is stored. Re-encoding is what strips EXIF/GPS/XMP and defeats polyglots, so the pipeline
/// never copies a segment through. Every limit is checked from the header BEFORE any pixel is decoded, because a
/// 40 KB PNG can declare 20000×20000 and a decode would allocate 1.6 GB on a replica customers share. At most two
/// decodes run at once for the same reason.
/// </remarks>
public sealed class ImagePipeline
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxSide = 8000;
    public const long MaxPixels = 40_000_000;
    public const int FullMaxSide = 1600;
    public const int ThumbMaxSide = 480;
    public const int JpegQuality = 85;

    private static readonly SemaphoreSlim Gate = new(2);

    public async Task<ImagePipelineResult> ProcessAsync(byte[] input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Length > MaxBytes)
            return ImagePipelineResult.Rejected(ImageRejections.TooLarge);

        if (!HasSupportedSignature(input))
            return ImagePipelineResult.Rejected(ImageRejections.UnsupportedFormat);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            return Process(input);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static bool HasSupportedSignature(ReadOnlySpan<byte> bytes) =>
        IsJpeg(bytes) || IsPng(bytes) || IsWebP(bytes);

    private static bool IsJpeg(ReadOnlySpan<byte> b) => b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

    private static bool IsPng(ReadOnlySpan<byte> b) =>
        b.Length >= 8 && b[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

    private static bool IsWebP(ReadOnlySpan<byte> b) =>
        b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8);

    private static ImagePipelineResult Process(byte[] input)
    {
        using var data = SKData.CreateCopy(input);
        using var codec = SKCodec.Create(data);
        if (codec is null)
            return ImagePipelineResult.Rejected(ImageRejections.Undecodable);

        var declared = codec.Info;
        if (declared.Width <= 0 || declared.Height <= 0)
            return ImagePipelineResult.Rejected(ImageRejections.Undecodable);
        if (declared.Width > MaxSide || declared.Height > MaxSide)
            return ImagePipelineResult.Rejected(ImageRejections.DimensionExceeded);
        if ((long)declared.Width * declared.Height > MaxPixels)
            return ImagePipelineResult.Rejected(ImageRejections.TooManyPixels);

        using var decoded = Decode(codec, declared);
        if (decoded is null)
            return ImagePipelineResult.Rejected(ImageRejections.Undecodable);

        using var source = SKImage.FromBitmap(decoded);
        var origin = codec.EncodedOrigin;

        var (full, width, height) = Render(source, origin, FullMaxSide);
        var (thumb, _, _) = Render(source, origin, ThumbMaxSide);

        var hash = Convert.ToHexString(SHA256.HashData(full)).ToLowerInvariant();
        return new ImagePipelineResult(new ProcessedImage(full, width, height, thumb, hash), null);
    }

    /// <summary>
    /// Decodes the first frame only, converted to sRGB, at the smallest size the codec supports that is still at
    /// least as large as the full variant — JPEG can decode at 1/2, 1/4 and 1/8 scale without the full buffer.
    /// </summary>
    private static SKBitmap? Decode(SKCodec codec, SKImageInfo declared)
    {
        var longest = Math.Max(declared.Width, declared.Height);
        var width = declared.Width;
        var height = declared.Height;

        if (longest > FullMaxSide)
        {
            var scale = (float)FullMaxSide / longest;
            var scaled = codec.GetScaledDimensions(scale);
            if (scaled.Width >= (int)Math.Ceiling(declared.Width * scale)
                && scaled.Height >= (int)Math.Ceiling(declared.Height * scale))
            {
                width = scaled.Width;
                height = scaled.Height;
            }
        }

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result == SKCodecResult.Success)
            return bitmap;

        bitmap.Dispose();
        return null;
    }

    private static (byte[] Bytes, int Width, int Height) Render(SKImage source, SKEncodedOrigin origin, int maxSide)
    {
        var swapsAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var orientedWidth = swapsAxes ? source.Height : source.Width;
        var orientedHeight = swapsAxes ? source.Width : source.Height;

        var scale = Math.Min(1.0, (double)maxSide / Math.Max(orientedWidth, orientedHeight));
        var width = Math.Max(1, (int)Math.Round(orientedWidth * scale));
        var height = Math.Max(1, (int)Math.Round(orientedHeight * scale));

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        // JPEG has no alpha; a transparent PNG would otherwise flatten onto black.
        canvas.Clear(SKColors.White);
        canvas.Scale((float)width / orientedWidth, (float)height / orientedHeight);
        var orientation = OrientationMatrix(origin, source.Width, source.Height);
        canvas.Concat(in orientation);
        canvas.DrawImage(source, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell));

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return (encoded.ToArray(), width, height);
    }

    /// <summary>
    /// Applies the EXIF orientation into the pixels, because re-encoding discards the tag that told a viewer to
    /// rotate — without this every portrait phone photo would be served on its side.
    /// </summary>
    private static SKMatrix OrientationMatrix(SKEncodedOrigin origin, float w, float h) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        _ => SKMatrix.Identity
    };
}
