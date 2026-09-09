using System.Globalization;
using System.Net;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Tests.Infrastructure;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Models;

[Collection(nameof(CultureSensitiveCollection))]
public class MobileErrorTests : IDisposable
{
    private readonly CultureInfo? _originalCulture = AppResources.Culture;

    public void Dispose() => AppResources.Culture = _originalCulture;

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, MobileErrorCategory.Validation)]
    [InlineData(HttpStatusCode.Forbidden, MobileErrorCategory.Forbidden)]
    [InlineData(HttpStatusCode.Conflict, MobileErrorCategory.Conflict)]
    [InlineData(HttpStatusCode.NotFound, MobileErrorCategory.NotFound)]
    [InlineData(HttpStatusCode.BadGateway, MobileErrorCategory.Unavailable)]
    [InlineData(HttpStatusCode.InternalServerError, MobileErrorCategory.Unknown)]
    public void StatusMapsToStableCategory(HttpStatusCode status, MobileErrorCategory expected)
    {
        var error = MobileError.FromStatus(MobileOperation.Booking, status);

        Assert.Equal(expected, error.Category);
    }

    [Theory]
    [InlineData("en", "That time is no longer available. Pick another.")]
    [InlineData("es-MX", "Ese horario ya no está disponible. Elige otro.")]
    public void BookingConflictUsesLocalizedActionableCopy(string culture, string expected)
    {
        AppResources.Culture = new CultureInfo(culture);

        var error = MobileError.FromStatus(
            MobileOperation.Booking,
            HttpStatusCode.Conflict,
            "Provider has overlapping appointment 64f0c2f1.");

        Assert.Equal(expected, error.Message);
        Assert.Equal("Provider has overlapping appointment 64f0c2f1.", error.DiagnosticDetail);
        Assert.DoesNotContain("64f0c2f1", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en", "Remove or complete active appointments for this profession, then try again.")]
    [InlineData("es-MX", "Elimina o completa las citas activas de esta profesión e intenta de nuevo.")]
    public void ProfessionConflictUsesLocalizedActionableCopy(string culture, string expected)
    {
        AppResources.Culture = new CultureInfo(culture);

        var error = MobileError.FromStatus(
            MobileOperation.ProfessionRemoval,
            HttpStatusCode.Conflict,
            "Cannot remove profession while appointments are active.");

        Assert.Equal(expected, error.Message);
    }

    [Theory]
    [InlineData("en", "Could not reach the server. Check your connection and try again.")]
    [InlineData("es-MX", "No pudimos comunicarnos con el servidor. Revisa tu conexión e intenta de nuevo.")]
    public void UnavailableUsesLocalizedRecoveryCopy(string culture, string expected)
    {
        AppResources.Culture = new CultureInfo(culture);

        Assert.Equal(expected, MobileError.Unavailable(MobileOperation.WorkWeek).Message);
    }
}