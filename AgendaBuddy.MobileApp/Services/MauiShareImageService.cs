#if MOBILE
using AgendaBuddy.MobileApp.Infrastructure;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace AgendaBuddy.MobileApp.Services;

/// <summary>
/// Draws a share image with Maui.Graphics at the pixel size <see cref="ShareImageLayout"/> dictates, writes it to
/// the cache directory, and hands it to the system share sheet.
/// </summary>
/// <remarks>
/// The share sheet does not report whether the person shared or dismissed it, so a completed request is
/// reported as shared and only an explicit cancellation as cancelled. Neither is ever an error.
/// </remarks>
public sealed class MauiShareImageService : IShareImageService
{
    public async Task<ShareOutcome> ShareAsync(
        ShareFormat format, ShareCardText text, string qrPayload, CancellationToken ct = default)
    {
        try
        {
            var path = await Task.Run(() => ShareImageComposer.WriteToCache(format, text, qrPayload), ct);
            await MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = text.Name,
                File = new ShareFile(path, "image/png")
            }));
            return ShareOutcome.Shared;
        }
        catch (OperationCanceledException)
        {
            return ShareOutcome.Cancelled;
        }
        catch (Exception)
        {
            return ShareOutcome.Failed;
        }
    }
}

/// <summary>Renders a <see cref="ShareLayout"/> to PNG bytes.</summary>
public static class ShareImageComposer
{
    private static readonly Color Background = Color.FromArgb("#FFFFFF");
    private static readonly Color Ink = Color.FromArgb("#111827");
    private static readonly Color Accent = Color.FromArgb("#4F46E5");
    private static readonly Color Muted = Color.FromArgb("#4B5563");

    public static string WriteToCache(ShareFormat format, ShareCardText text, string qrPayload)
    {
        var directory = Path.Combine(FileSystem.CacheDirectory, "share");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"agendame-{format.ToString().ToLowerInvariant()}.png");
        File.WriteAllBytes(path, Render(format, text, qrPayload));
        return path;
    }

    public static byte[] Render(ShareFormat format, ShareCardText text, string qrPayload)
    {
        var layout = ShareImageLayout.For(format);
        using var context = new PlatformBitmapExportService().CreateContext(layout.Width, layout.Height, 1f);
        var canvas = context.Canvas;

        canvas.FillColor = Background;
        canvas.FillRectangle(0, 0, layout.Width, layout.Height);

        DrawText(canvas, text.Brand, layout.Brand, layout.BrandFontSize, Accent, bold: true);
        DrawText(canvas, text.Name, layout.Name, layout.NameFontSize, Ink, bold: true);
        DrawQr(canvas, qrPayload, layout.Qr);
        DrawText(canvas, text.Code, layout.Code, layout.CodeFontSize, Ink, bold: true);
        DrawText(canvas, text.StepOne + "\n" + text.StepTwo, layout.Instruction, layout.InstructionFontSize, Muted,
            bold: false);

        using var stream = new MemoryStream();
        context.WriteToStream(stream);
        return stream.ToArray();
    }

    private static void DrawText(ICanvas canvas, string value, LayoutRect rect, float size, Color color, bool bold)
    {
        canvas.FontColor = color;
        canvas.FontSize = size;
        canvas.Font = bold ? Microsoft.Maui.Graphics.Font.DefaultBold : Microsoft.Maui.Graphics.Font.Default;
        canvas.DrawString(value, rect.X, rect.Y, rect.Width, rect.Height,
            HorizontalAlignment.Center, VerticalAlignment.Center, TextFlow.ClipBounds);
    }

    private static void DrawQr(ICanvas canvas, string payload, LayoutRect rect)
    {
        var modules = ShowcaseQr.Modules(payload);
        var count = modules.GetLength(0);
        if (count == 0)
            return;

        var cell = Math.Min(rect.Width, rect.Height) / count;
        canvas.FillColor = Ink;
        for (var y = 0; y < count; y++)
        {
            for (var x = 0; x < count; x++)
            {
                if (modules[y, x])
                    canvas.FillRectangle(rect.X + x * cell, rect.Y + y * cell, cell + 0.5f, cell + 0.5f);
            }
        }
    }
}
#endif
