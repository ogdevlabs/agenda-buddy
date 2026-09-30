using System.Globalization;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

[Collection(nameof(CultureSensitiveCollection))]
public class ShowcaseErrorCopyTests
{
    public static TheoryData<string, string, string> Copy => new()
    {
        { ShowcaseErrorCodes.UnsupportedFormat, "Use a JPEG, PNG or WebP photo.", "Usa una foto JPEG, PNG o WebP." },
        { ShowcaseErrorCodes.TooLarge, "This photo is too large. Try a smaller one.", "Esta foto es demasiado grande. Prueba con una más pequeña." },
        { ShowcaseErrorCodes.TooManyPixels, "This photo is too large. Try a smaller one.", "Esta foto es demasiado grande. Prueba con una más pequeña." },
        { ShowcaseErrorCodes.DimensionExceeded, "This photo is too large. Try a smaller one.", "Esta foto es demasiado grande. Prueba con una más pequeña." },
        { ShowcaseErrorCodes.Undecodable, "This file couldn't be opened as a photo.", "Este archivo no se pudo abrir como foto." },
        { ShowcaseErrorCodes.UnknownMedia, "That photo is no longer available. Upload it again.", "Esa foto ya no está disponible. Súbela de nuevo." },
        { ShowcaseErrorCodes.StorageUnavailable, "Photos can't be saved right now. Try again in a few minutes.", "Ahora no se pueden guardar fotos. Intenta de nuevo en unos minutos." },
        { ShowcaseErrorCodes.PortfolioFull, "Your portfolio already has 20 images. Remove one to add another.", "Tu portafolio ya tiene 20 imágenes. Quita una para agregar otra." },
        { ShowcaseErrorCodes.PortfolioChanged, "Your portfolio changed on another device. We've reloaded it — try again.", "Tu portafolio cambió en otro dispositivo. Lo volvimos a cargar; intenta de nuevo." },
        { ShowcaseErrorCodes.ShowcaseNotFound, "This provider isn't available.", "Este proveedor no está disponible." },
        { ShowcaseErrorCodes.RateLimited, "Too many attempts. Wait a moment and try again.", "Demasiados intentos. Espera un momento e intenta de nuevo." },
        { ShowcaseErrorCodes.Invalid, "Something in that request wasn't accepted. Check it and try again.", "Algo en esa solicitud no fue aceptado. Revísalo e intenta de nuevo." },
        { ShowcaseErrorCodes.Network, "Couldn't reach AgendaMe. Check your connection and try again.", "No se pudo conectar con AgendaMe. Revisa tu conexión e intenta de nuevo." },
        { ShowcaseErrorCodes.Unknown, "Something went wrong. Try again.", "Algo salió mal. Intenta de nuevo." }
    };

    [Theory]
    [MemberData(nameof(Copy))]
    public void EveryCodeHasEnglishAndSpanishCopy(string code, string english, string spanish)
    {
        Assert.Equal(english, In("en", () => ShowcaseErrorCopy.For(code)));
        Assert.Equal(spanish, In("es-MX", () => ShowcaseErrorCopy.For(code)));
    }

    [Fact]
    public void TheTheoryCoversEveryCode() =>
        Assert.Equal(
            ShowcaseErrorCodes.All.Order(),
            Copy.Select(row => (string)row[0]).Order());

    [Fact]
    public void EveryCodeHasItsOwnKeyExceptThroughTheGenericFallback() =>
        Assert.Equal(ShowcaseErrorCodes.All.Count, ShowcaseErrorCodes.All.Select(ShowcaseErrorCopy.KeyFor).Distinct().Count());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something-new")]
    public void AnUnrecognisedCodeFallsBackToTheGenericLine(string? code) =>
        Assert.Equal("Something went wrong. Try again.", In("en", () => ShowcaseErrorCopy.For(code)));

    [Fact]
    public void AnUnreachableDestinationIsNamedRatherThanDescribedAsStorage()
    {
        var result = ShowcaseResult<bool>.Fail(ShowcaseErrorCodes.StorageUnavailable, 503, "provider");

        var text = In("en", () => ShowcaseErrorCopy.Describe(result));

        Assert.NotEqual(In("en", () => ShowcaseErrorCopy.For(ShowcaseErrorCodes.StorageUnavailable)), text);
        Assert.Contains("unavailable", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string In(string culture, Func<string> read)
    {
        var original = AppResources.Culture;
        try
        {
            AppResources.Culture = new CultureInfo(culture);
            return read();
        }
        finally
        {
            AppResources.Culture = original;
        }
    }
}
