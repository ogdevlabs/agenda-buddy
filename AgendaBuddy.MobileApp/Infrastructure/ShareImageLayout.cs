using QRCoder;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>The three images a provider can export to share their code.</summary>
public enum ShareFormat
{
    /// <summary>1080×1920, for a story.</summary>
    Story,

    /// <summary>1080×1080, for a feed post.</summary>
    Square,

    /// <summary>A 3.5×2 inch card at 300 dpi, for print.</summary>
    Card
}

public readonly record struct LayoutRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public bool Intersects(LayoutRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    public bool IsInside(float width, float height) => X >= 0 && Y >= 0 && Right <= width && Bottom <= height;
}

/// <summary>Where each element of a share image goes, in pixels. Drawing is the platform's job; placement is here.</summary>
public sealed record ShareLayout(
    ShareFormat Format,
    int Width,
    int Height,
    LayoutRect Brand,
    LayoutRect Name,
    LayoutRect Qr,
    LayoutRect Code,
    LayoutRect Instruction,
    float BrandFontSize,
    float NameFontSize,
    float CodeFontSize,
    float InstructionFontSize)
{
    public IEnumerable<LayoutRect> Elements => [Brand, Name, Qr, Code, Instruction];
}

/// <summary>The words on a share image. Every format carries the code and both steps.</summary>
/// <remarks>
/// A phone-camera scan of the QR only reaches the store, so the customer must scan again inside the app after
/// installing. The image is the only thing that can tell them so before they install — hence both steps, and the
/// code printed large enough to type when a second scan is not possible.
/// </remarks>
public sealed record ShareCardText(string Brand, string Name, string Code, string StepOne, string StepTwo)
{
    public static ShareCardText For(string brand, string providerName, string code)
    {
        var display = ShowcaseCodeParser.FormatForDisplay(code);
        return new ShareCardText(
            brand,
            providerName,
            display,
            AppResources.Format("Showcase_ShareStepOne", brand),
            AppResources.Format("Showcase_ShareStepTwo", display));
    }
}

public static class ShareImageLayout
{
    public static ShareLayout For(ShareFormat format) => format switch
    {
        ShareFormat.Story => Story(),
        ShareFormat.Square => Square(),
        _ => Card()
    };

    public static (int Width, int Height) SizeOf(ShareFormat format) => format switch
    {
        ShareFormat.Story => (1080, 1920),
        ShareFormat.Square => (1080, 1080),
        _ => (1050, 600)
    };

    private static ShareLayout Story()
    {
        const int w = 1080, h = 1920, margin = 90;
        const float qr = 720;
        return new ShareLayout(ShareFormat.Story, w, h,
            Brand: new LayoutRect(margin, 140, w - 2 * margin, 90),
            Name: new LayoutRect(margin, 280, w - 2 * margin, 150),
            Qr: new LayoutRect((w - qr) / 2, 480, qr, qr),
            Code: new LayoutRect(margin, 1250, w - 2 * margin, 170),
            Instruction: new LayoutRect(margin, 1470, w - 2 * margin, 300),
            BrandFontSize: 64, NameFontSize: 72, CodeFontSize: 132, InstructionFontSize: 44);
    }

    private static ShareLayout Square()
    {
        const int w = 1080, h = 1080, margin = 60;
        const float qr = 480;
        return new ShareLayout(ShareFormat.Square, w, h,
            Brand: new LayoutRect(margin, 40, w - 2 * margin, 60),
            Name: new LayoutRect(margin, 110, w - 2 * margin, 80),
            Qr: new LayoutRect((w - qr) / 2, 210, qr, qr),
            Code: new LayoutRect(margin, 710, w - 2 * margin, 120),
            Instruction: new LayoutRect(margin, 850, w - 2 * margin, 190),
            BrandFontSize: 44, NameFontSize: 52, CodeFontSize: 96, InstructionFontSize: 34);
    }

    private static ShareLayout Card()
    {
        const int w = 1050, h = 600, margin = 45;
        const float qr = 420;
        const float textX = margin + qr + 40;
        const float textWidth = w - textX - margin;
        return new ShareLayout(ShareFormat.Card, w, h,
            Brand: new LayoutRect(textX, margin, textWidth, 50),
            Name: new LayoutRect(textX, 105, textWidth, 70),
            Qr: new LayoutRect(margin, (h - qr) / 2, qr, qr),
            Code: new LayoutRect(textX, 190, textWidth, 100),
            Instruction: new LayoutRect(textX, 310, textWidth, 245),
            BrandFontSize: 36, NameFontSize: 42, CodeFontSize: 78, InstructionFontSize: 26);
    }
}

/// <summary>QR generation, on-device. The payload is the provider's <c>/go/{code}</c> URL.</summary>
public static class ShowcaseQr
{
    /// <summary>
    /// The module grid, quiet zone included, <c>true</c> for a dark module. Error correction Q, so a printed card
    /// with a crease or a smudge still scans.
    /// </summary>
    public static bool[,] Modules(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var rows = data.ModuleMatrix;
        var size = rows.Count;
        var modules = new bool[size, size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
                modules[y, x] = rows[y][x];
        }

        return modules;
    }

    /// <summary>A PNG of the code, for showing on screen.</summary>
    public static byte[] Png(string payload, int pixelsPerModule = 12)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }
}
