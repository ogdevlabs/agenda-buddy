using System.Globalization;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Resources.Strings;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

[Collection(nameof(CultureSensitiveCollection))]
public class ShareImageLayoutTests
{
    public static TheoryData<ShareFormat> Formats => new() { ShareFormat.Story, ShareFormat.Square, ShareFormat.Card };

    [Theory]
    [InlineData(ShareFormat.Story, 1080, 1920)]
    [InlineData(ShareFormat.Square, 1080, 1080)]
    [InlineData(ShareFormat.Card, 1050, 600)]
    public void EachFormatHasItsPublishedSize(ShareFormat format, int width, int height)
    {
        var layout = ShareImageLayout.For(format);

        Assert.Equal((width, height), (layout.Width, layout.Height));
        Assert.Equal((width, height), ShareImageLayout.SizeOf(format));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void EveryElementFitsInsideTheImage(ShareFormat format)
    {
        var layout = ShareImageLayout.For(format);

        Assert.All(layout.Elements, rect => Assert.True(rect.IsInside(layout.Width, layout.Height), $"{format}: {rect}"));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void NoTwoElementsOverlap(ShareFormat format)
    {
        var elements = ShareImageLayout.For(format).Elements.ToList();

        for (var i = 0; i < elements.Count; i++)
        {
            for (var j = i + 1; j < elements.Count; j++)
                Assert.False(elements[i].Intersects(elements[j]), $"{format}: {elements[i]} overlaps {elements[j]}");
        }
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void TheQrIsSquareAndLargeEnoughToScan(ShareFormat format)
    {
        var layout = ShareImageLayout.For(format);

        Assert.Equal(layout.Qr.Width, layout.Qr.Height);
        Assert.True(layout.Qr.Width >= 400, $"{format} QR is {layout.Qr.Width}px");
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void TheCodeIsTheLargestText(ShareFormat format)
    {
        var layout = ShareImageLayout.For(format);

        Assert.True(layout.CodeFontSize > layout.NameFontSize);
        Assert.True(layout.CodeFontSize > layout.InstructionFontSize);
        Assert.True(layout.CodeFontSize <= layout.Code.Height);
    }

    [Fact]
    public void TheCardTextCarriesTheCodeAndBothSteps()
    {
        var original = AppResources.Culture;
        try
        {
            AppResources.Culture = new CultureInfo("en");
            var english = ShareCardText.For(AppBrand.Name, "Mariana Ruiz", "K7Q2X9");
            Assert.Equal("K7Q 2X9", english.Code);
            Assert.Equal($"1 · Scan to get {AppBrand.Name}", english.StepOne);
            Assert.Equal("2 · In the app, tap Scan a code — or type K7Q 2X9", english.StepTwo);

            AppResources.Culture = new CultureInfo("es-MX");
            var spanish = ShareCardText.For(AppBrand.Name, "Mariana Ruiz", "K7Q2X9");
            Assert.Equal($"1 · Escanea para descargar {AppBrand.Name}", spanish.StepOne);
            Assert.Equal("2 · En la app, toca Escanear código — o escribe K7Q 2X9", spanish.StepTwo);
        }
        finally
        {
            AppResources.Culture = original;
        }
    }

    [Fact]
    public void TheTextShareCarriesTheNameAndBothSteps()
    {
        var text = new ShareCardText("AgendaMe", "Mariana Ruiz", "K7Q 2X9", "1 · one", "2 · two K7Q 2X9").ToPlainText();

        Assert.Equal(string.Join(Environment.NewLine, "Mariana Ruiz", "1 · one", "2 · two K7Q 2X9"), text);
    }

    [Fact]
    public void TheQrEncodesThePayloadAsASquareModuleGrid()
    {
        var modules = ShowcaseQr.Modules("https://agendame.app/api/v1/go/K7Q2X9");

        Assert.Equal(modules.GetLength(0), modules.GetLength(1));
        Assert.True(modules.GetLength(0) >= 21);
        Assert.Contains(true, modules.Cast<bool>());
        Assert.Contains(false, modules.Cast<bool>());
    }

    [Fact]
    public void TheOnScreenQrIsAPng()
    {
        var png = ShowcaseQr.Png("https://agendame.app/api/v1/go/K7Q2X9");

        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png.Take(4).ToArray());
    }
}
