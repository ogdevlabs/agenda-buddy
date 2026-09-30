using System.Globalization;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

[Collection(nameof(CultureSensitiveCollection))]
public class ShowcaseTextTests
{
    private static ShowcaseFunnel Funnel(int scans, int opened, int booked, int window = 7) =>
        new() { Scans = scans, Opened = opened, Booked = booked, WindowDays = window };

    [Fact]
    public void ManyReadsAsOneSentence()
    {
        Assert.Equal(
            "This week, 41 people scanned your code, 17 opened your showcase and 4 booked.",
            In("en", () => ShowcaseText.FunnelSentence(Funnel(41, 17, 4))));
        Assert.Equal(
            "Esta semana, 41 personas escanearon tu código, 17 abrieron tu vitrina y 4 reservaron.",
            In("es-MX", () => ShowcaseText.FunnelSentence(Funnel(41, 17, 4))));
    }

    [Fact]
    public void OneAgreesInTheSingular()
    {
        Assert.Equal(
            "This week, 1 person scanned your code, 1 opened your showcase and 1 booked.",
            In("en", () => ShowcaseText.FunnelSentence(Funnel(1, 1, 1))));
        Assert.Equal(
            "Esta semana, 1 persona escaneó tu código, 1 abrió tu vitrina y 1 reservó.",
            In("es-MX", () => ShowcaseText.FunnelSentence(Funnel(1, 1, 1))));
    }

    [Fact]
    public void AZeroCountBesideOthersIsWordedNotNumbered()
    {
        Assert.Equal(
            "This week, 3 people scanned your code, 1 opened your showcase and no one booked.",
            In("en", () => ShowcaseText.FunnelSentence(Funnel(3, 1, 0))));
        Assert.Equal(
            "Esta semana, 3 personas escanearon tu código, 1 abrió tu vitrina y nadie reservó.",
            In("es-MX", () => ShowcaseText.FunnelSentence(Funnel(3, 1, 0))));
    }

    [Fact]
    public void NothingYetSaysWhatToDoNext()
    {
        Assert.Equal(
            "No one has scanned your code yet. Share it from QR & Share.",
            In("en", () => ShowcaseText.FunnelSentence(Funnel(0, 0, 0))));
        Assert.Equal(
            "Nadie ha escaneado tu código todavía. Compártelo desde QR y compartir.",
            In("es-MX", () => ShowcaseText.FunnelSentence(Funnel(0, 0, 0))));
        Assert.Equal(
            In("en", () => ShowcaseText.FunnelSentence(Funnel(0, 0, 0))),
            In("en", () => ShowcaseText.FunnelSentence(null)));
    }

    [Fact]
    public void AnotherWindowIsNamed()
    {
        Assert.StartsWith("In the last 30 days, ", In("en", () => ShowcaseText.FunnelSentence(Funnel(2, 0, 0, 30))));
        Assert.StartsWith("En los últimos 30 días, ", In("es-MX", () => ShowcaseText.FunnelSentence(Funnel(2, 0, 0, 30))));
    }

    [Fact]
    public void CompletenessAndPortfolioCountRead()
    {
        var completeness = new ShowcaseCompleteness { Done = 3, Total = 5 };
        Assert.Equal("3 of 5 done", In("en", () => ShowcaseText.Completeness(completeness)));
        Assert.Equal("3 de 5 listos", In("es-MX", () => ShowcaseText.Completeness(completeness)));
        Assert.Equal(0.6, ShowcaseText.CompletenessProgress(completeness), 3);
        Assert.Equal(0, ShowcaseText.CompletenessProgress(null));
        Assert.Equal(1, ShowcaseText.CompletenessProgress(new ShowcaseCompleteness { Done = 9, Total = 5 }));

        Assert.Equal("18 of 20", In("en", () => ShowcaseText.PortfolioCount(18)));
        Assert.Equal("18 de 20", In("es-MX", () => ShowcaseText.PortfolioCount(18)));
        Assert.Equal(2, ShowcaseText.RemainingSlots(18));
        Assert.Equal(0, ShowcaseText.RemainingSlots(25));
    }

    [Fact]
    public void NameBasedCopyUsesTheFirstNameAndNeverAPronoun()
    {
        Assert.Equal("See Mariana's work", In("en", () => ShowcaseText.SeeWork("Mariana")));
        Assert.Equal("Ver el trabajo de Mariana", In("es-MX", () => ShowcaseText.SeeWork("Mariana")));
        Assert.Equal("Opened from Mariana's code", In("en", () => ShowcaseText.OpenedFromCode(" Mariana ")));
        Assert.Equal("Abierto desde el código de Mariana", In("es-MX", () => ShowcaseText.OpenedFromCode("Mariana")));
        Assert.Equal("Mariana is hidden", In("en", () => ShowcaseText.Hidden("Mariana")));

        Assert.Equal("See their work", In("en", () => ShowcaseText.SeeWork(null)));
        Assert.Equal("Opened from a provider's code", In("en", () => ShowcaseText.OpenedFromCode("")));
        Assert.Equal("Provider hidden", In("en", () => ShowcaseText.Hidden(" ")));
    }

    [Fact]
    public void TileDescriptionsGiveThePositionAndCaption()
    {
        Assert.Equal("Photo 3 of 12", In("en", () => ShowcaseText.TileDescription(3, 12, null)));
        Assert.Equal("Photo 3 of 12, Botanical sleeve", In("en", () => ShowcaseText.TileDescription(3, 12, "Botanical sleeve")));
        Assert.Equal("Foto 3 de 12", In("es-MX", () => ShowcaseText.TileDescription(3, 12, "")));
    }

    [Fact]
    public void TheNextSessionNamesTheServiceWhenThereIsOne()
    {
        var appointment = new ShowcaseNextAppointment
        {
            ScheduledAt = new DateTime(2026, 10, 12, 16, 0, 0, DateTimeKind.Utc),
            ServiceName = "Fine-line piece"
        };

        var withService = In("en", () => ShowcaseText.NextSession(appointment));
        Assert.StartsWith("Your next session: ", withService);
        Assert.EndsWith(" · Fine-line piece", withService);

        appointment.ServiceName = null;
        Assert.DoesNotContain("·", In("en", () => ShowcaseText.NextSession(appointment)));
        Assert.StartsWith("Tu próxima sesión: ", In("es-MX", () => ShowcaseText.NextSession(appointment)));
    }

    [Theory]
    [InlineData("Mariana Ruiz", "Mariana")]
    [InlineData("  Ana  ", "Ana")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void FirstNameOfTakesTheFirstWord(string? full, string expected) =>
        Assert.Equal(expected, ShowcaseText.FirstNameOf(full));

    [Fact]
    public void VisibleLengthCountsAnEmojiAsOne()
    {
        Assert.Equal(3, ShowcaseText.VisibleLength("ab👍🏽"));
        Assert.Equal(0, ShowcaseText.VisibleLength(null));
        Assert.Equal(2, ShowcaseText.VisibleLength("  ab "));
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
