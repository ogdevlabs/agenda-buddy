using System.Buffers.Binary;
using SkiaSharp;

namespace AgendaBuddy.Library.Tests.Media;

internal static class TestImages
{
    public static byte[] Encode(int width, int height, SKEncodedImageFormat format, SKColor? fill = null,
        int quality = 90)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
            canvas.Clear(fill ?? SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    /// <summary>Left half red, right half blue, so a rotation is observable in the output pixels.</summary>
    public static byte[] SplitJpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            using var red = new SKPaint { Color = SKColors.Red };
            using var blue = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(0, 0, width / 2f, height, red);
            canvas.DrawRect(width / 2f, 0, width / 2f, height, blue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        return data.ToArray();
    }

    /// <summary>
    /// Inserts an APP1 Exif segment right after SOI carrying an orientation tag and a trailing marker string that
    /// stands in for GPS or other metadata that must never be served back.
    /// </summary>
    public static byte[] WithExif(byte[] jpeg, ushort orientation, string marker)
    {
        var tiff = new List<byte>();
        tiff.AddRange("MM"u8.ToArray());
        tiff.AddRange(new byte[] { 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08 });
        tiff.AddRange(new byte[] { 0x00, 0x01 });
        tiff.AddRange(new byte[] { 0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01 });
        tiff.AddRange(new[] { (byte)(orientation >> 8), (byte)orientation, (byte)0x00, (byte)0x00 });
        tiff.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        tiff.AddRange(System.Text.Encoding.ASCII.GetBytes(marker));

        var payload = new List<byte>();
        payload.AddRange("Exif\0\0"u8.ToArray());
        payload.AddRange(tiff);

        var length = payload.Count + 2;
        var segment = new List<byte> { 0xFF, 0xE1, (byte)(length >> 8), (byte)length };
        segment.AddRange(payload);

        var result = new List<byte> { jpeg[0], jpeg[1] };
        result.AddRange(segment);
        result.AddRange(jpeg.Skip(2));
        return result.ToArray();
    }

    /// <summary>A real PNG whose IHDR is rewritten to declare other dimensions, CRC recomputed.</summary>
    public static byte[] PngDeclaring(int width, int height)
    {
        var png = Encode(4, 4, SKEncodedImageFormat.Png);
        // Signature (8) + length (4) + "IHDR" (4), then width and height.
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20, 4), height);
        var crc = Crc32(png.AsSpan(12, 4 + 13));
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(29, 4), crc);
        return png;
    }

    public static bool Contains(byte[] haystack, ReadOnlySpan<byte> needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
