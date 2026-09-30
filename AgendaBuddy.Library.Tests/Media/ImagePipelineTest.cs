using System.Security.Cryptography;
using AgendaBuddy.Library.Media;
using AgendaBuddy.Library.Showcase;
using SkiaSharp;
using Xunit;

namespace AgendaBuddy.Library.Tests.Media;

public class ImagePipelineTest
{
    private readonly ImagePipeline _pipeline = new();

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public async Task ProcessAsync_ASupportedFormat_IsReEncodedAsJpeg(SKEncodedImageFormat format)
    {
        var input = TestImages.Encode(320, 200, format);

        var result = await _pipeline.ProcessAsync(input);

        Assert.Null(result.Rejection);
        var image = Assert.IsType<ProcessedImage>(result.Image);
        Assert.Equal(320, image.Width);
        Assert.Equal(200, image.Height);
        AssertIsJpeg(image.Full);
        AssertIsJpeg(image.Thumb);
    }

    [Fact]
    public async Task ProcessAsync_AnUnsupportedSignature_IsRejected()
    {
        var gif = "GIF89a"u8.ToArray().Concat(new byte[64]).ToArray();

        var result = await _pipeline.ProcessAsync(gif);

        Assert.Null(result.Image);
        Assert.Equal(ImageRejections.UnsupportedFormat, result.Rejection);
    }

    [Fact]
    public async Task ProcessAsync_ASupportedSignatureThatCannotBeDecoded_IsUndecodable()
    {
        var garbage = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8 };

        var result = await _pipeline.ProcessAsync(garbage);

        Assert.Null(result.Image);
        Assert.Equal(ImageRejections.Undecodable, result.Rejection);
    }

    [Fact]
    public async Task ProcessAsync_MoreThanFiveMegabytes_IsTooLargeBeforeAnythingElse()
    {
        var input = new byte[ImagePipeline.MaxBytes + 1];
        input[0] = 0xFF;
        input[1] = 0xD8;
        input[2] = 0xFF;

        var result = await _pipeline.ProcessAsync(input);

        Assert.Equal(ImageRejections.TooLarge, result.Rejection);
    }

    [Fact]
    public async Task ProcessAsync_ExactlyFiveMegabytesOfGarbage_IsNotTooLarge()
    {
        var result = await _pipeline.ProcessAsync(new byte[ImagePipeline.MaxBytes]);

        Assert.Equal(ImageRejections.UnsupportedFormat, result.Rejection);
    }

    [Theory]
    [InlineData(8001, 10)]
    [InlineData(10, 8001)]
    [InlineData(20000, 20000)]
    public async Task ProcessAsync_ASideOverTheLimit_IsDimensionExceeded(int width, int height)
    {
        var result = await _pipeline.ProcessAsync(TestImages.PngDeclaring(width, height));

        Assert.Equal(ImageRejections.DimensionExceeded, result.Rejection);
    }

    [Fact]
    public async Task ProcessAsync_MoreThanFortyMegapixelsWithinTheSideLimit_IsTooManyPixels()
    {
        var result = await _pipeline.ProcessAsync(TestImages.PngDeclaring(8000, 5001));

        Assert.Equal(ImageRejections.TooManyPixels, result.Rejection);
    }

    [Fact]
    public async Task ProcessAsync_ALargeImage_IsDownscaledToTheFullAndThumbLimits()
    {
        var input = TestImages.Encode(3200, 1600, SKEncodedImageFormat.Jpeg);

        var image = (await _pipeline.ProcessAsync(input)).Image!;

        Assert.Equal(ImagePipeline.FullMaxSide, image.Width);
        Assert.Equal(800, image.Height);
        using var full = SKBitmap.Decode(image.Full);
        Assert.Equal(ImagePipeline.FullMaxSide, Math.Max(full.Width, full.Height));
        using var thumb = SKBitmap.Decode(image.Thumb);
        Assert.Equal(ImagePipeline.ThumbMaxSide, Math.Max(thumb.Width, thumb.Height));
        Assert.Equal(240, Math.Min(thumb.Width, thumb.Height));
    }

    [Fact]
    public async Task ProcessAsync_ASmallImage_IsNeverUpscaled()
    {
        var image = (await _pipeline.ProcessAsync(TestImages.Encode(100, 60, SKEncodedImageFormat.Png))).Image!;

        using var thumb = SKBitmap.Decode(image.Thumb);
        Assert.Equal(100, thumb.Width);
        Assert.Equal(60, thumb.Height);
    }

    [Fact]
    public async Task ProcessAsync_TheSameInput_ProducesTheSameHash()
    {
        var input = TestImages.Encode(400, 300, SKEncodedImageFormat.Png);

        var first = (await _pipeline.ProcessAsync(input)).Image!;
        var second = (await _pipeline.ProcessAsync(input)).Image!;

        Assert.Equal(first.Hash, second.Hash);
        Assert.True(ShowcaseRules.IsHash(first.Hash));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(first.Full)).ToLowerInvariant(), first.Hash);
    }

    [Fact]
    public async Task ProcessAsync_DifferentInputs_ProduceDifferentHashes()
    {
        var red = (await _pipeline.ProcessAsync(TestImages.Encode(64, 64, SKEncodedImageFormat.Png, SKColors.Red))).Image!;
        var blue = (await _pipeline.ProcessAsync(TestImages.Encode(64, 64, SKEncodedImageFormat.Png, SKColors.Blue))).Image!;

        Assert.NotEqual(red.Hash, blue.Hash);
    }

    [Fact]
    public async Task ProcessAsync_ExifMetadata_IsNeverCarriedThrough()
    {
        const string marker = "GPS-SECRET-LOCATION";
        var input = TestImages.WithExif(TestImages.SplitJpeg(200, 100), 1, marker);

        var image = (await _pipeline.ProcessAsync(input)).Image!;

        foreach (var output in new[] { image.Full, image.Thumb })
        {
            AssertIsJpeg(output);
            Assert.False(TestImages.Contains(output, "Exif\0\0"u8));
            Assert.False(TestImages.Contains(output, System.Text.Encoding.ASCII.GetBytes(marker)));
        }
    }

    [Fact]
    public async Task ProcessAsync_AnExifRotation_IsAppliedIntoThePixels()
    {
        // Orientation 6: the stored image must be rotated 90° clockwise to display, so the left edge becomes the top.
        var input = TestImages.WithExif(TestImages.SplitJpeg(200, 100), 6, "x");

        var image = (await _pipeline.ProcessAsync(input)).Image!;

        Assert.Equal(100, image.Width);
        Assert.Equal(200, image.Height);
        using var decoded = SKBitmap.Decode(image.Full);
        var top = decoded.GetPixel(50, 20);
        var bottom = decoded.GetPixel(50, 180);
        Assert.True(top.Red > 200 && top.Blue < 60, $"expected red at the top, got {top}");
        Assert.True(bottom.Blue > 200 && bottom.Red < 60, $"expected blue at the bottom, got {bottom}");
    }

    [Fact]
    public async Task ProcessAsync_ATransparentPng_FlattensOntoWhite()
    {
        var input = TestImages.Encode(50, 50, SKEncodedImageFormat.Png, SKColors.Transparent);

        var image = (await _pipeline.ProcessAsync(input)).Image!;

        using var decoded = SKBitmap.Decode(image.Full);
        var pixel = decoded.GetPixel(25, 25);
        Assert.True(pixel.Red >= 250 && pixel.Green >= 250 && pixel.Blue >= 250, $"expected white, got {pixel}");
    }

    [Fact]
    public void Describe_NamesEveryRejection()
    {
        Assert.Contains("5 MB", ImageRejections.Describe(ImageRejections.TooLarge));
        Assert.Contains("40 megapixels", ImageRejections.Describe(ImageRejections.TooManyPixels));
        Assert.Contains("8000", ImageRejections.Describe(ImageRejections.DimensionExceeded));
        Assert.Contains("JPEG", ImageRejections.Describe(ImageRejections.UnsupportedFormat));
        Assert.Contains("couldn't be opened", ImageRejections.Describe(ImageRejections.Undecodable));
    }

    private static void AssertIsJpeg(byte[] bytes)
    {
        Assert.True(bytes.Length > 3);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }
}
