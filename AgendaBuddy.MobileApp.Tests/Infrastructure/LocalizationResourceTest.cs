using System.Xml.Linq;
using AgendaBuddy.MobileApp.Resources.Strings;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

[Collection(nameof(CultureSensitiveCollection))]
public class LocalizationResourceTest
{
    private static readonly HashSet<string> IntentionallyLanguageNeutralKeys =
    [
        "Accessibility_Time",
        "Appointment_TimeAndDuration",
        "Duration_ManyMinutes",
        "Duration_OneMinute",
        "EmailVerification_CodePlaceholder",
        "Fee_WithType",
        "Profession_014",
        "Profession_018",
        "Profession_023",
        "Profession_072",
        "Profession_110"
    ];

    [Fact]
    public void EnglishAndSpanishResourcesHaveExactNonEmptyKeyParity()
    {
        var english = Load("AppResources.resx");
        var spanish = Load("AppResources.es-MX.resx");

        Assert.NotEmpty(english);
        Assert.Equal(english.Keys.Order(), spanish.Keys.Order());
        Assert.DoesNotContain(english, item => string.IsNullOrWhiteSpace(item.Value));
        Assert.DoesNotContain(spanish, item => string.IsNullOrWhiteSpace(item.Value));
    }

    [Fact]
    public void StronglyTypedResourcesFollowTheSelectedCulture()
    {
        var original = AppResources.Culture;

        try
        {
            AppResources.Culture = new System.Globalization.CultureInfo("en");
            Assert.Equal("Language", AppResources.Language_Title);

            AppResources.Culture = new System.Globalization.CultureInfo("es-MX");
            Assert.Equal("Idioma", AppResources.Language_Title);
        }
        finally
        {
            AppResources.Culture = original;
        }
    }

    [Fact]
    public void SpanishDoesNotSilentlyCopyEnglishText()
    {
        var english = Load("AppResources.resx");
        var spanish = Load("AppResources.es-MX.resx");
        var copied = english
            .Where(item => spanish[item.Key] == item.Value)
            .Select(item => item.Key)
            .Where(key => !IntentionallyLanguageNeutralKeys.Contains(key))
            .ToList();

        Assert.True(copied.Count == 0,
            "Spanish resources copy English text without an explicit locale-neutral decision: "
            + string.Join(", ", copied));
    }

    [Fact]
    public void RuntimeFormattingUsesTheSelectedCulture()
    {
        var original = AppResources.Culture;

        try
        {
            AppResources.Culture = new System.Globalization.CultureInfo("es-MX");

            Assert.Equal("lunes", AppResources.CurrentCulture.DateTimeFormat.GetDayName(DayOfWeek.Monday));
            Assert.Equal("El horario del lunes debe comenzar antes de terminar.",
                AppResources.Format(nameof(AppResources.Validation_DayMustStartBeforeEnd), "lunes"));
        }
        finally
        {
            AppResources.Culture = original;
        }
    }

    [Fact]
    public void AppointmentNavigationAndSegmentsUseTheApprovedEnglishAndSpanishLabels()
    {
        var english = Load("AppResources.resx");
        var spanish = Load("AppResources.es-MX.resx");

        Assert.Equal("Appointments", english["Xaml_Calendar"]);
        Assert.Equal("Scheduled", english["Appointments_Scheduled"]);
        Assert.Equal("Done", english["Appointments_Done"]);
        Assert.Equal("Cancelled", english["Appointments_Cancelled"]);

        Assert.Equal("Citas", spanish["Xaml_Calendar"]);
        Assert.Equal("Programadas", spanish["Appointments_Scheduled"]);
        Assert.Equal("Realizadas", spanish["Appointments_Done"]);
        Assert.Equal("Canceladas", spanish["Appointments_Cancelled"]);
    }

    private static Dictionary<string, string> Load(string fileName)
    {
        var path = Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "Resources", "Strings", fileName);
        var entries = XDocument.Load(path)
            .Root!
            .Elements("data")
            .Select(element => new
            {
                Name = (string?)element.Attribute("name"),
                Value = (string?)element.Element("value")
            })
            .Where(entry => entry.Name is not null)
            .ToList();

        Assert.Equal(entries.Count, entries.Select(entry => entry.Name).Distinct().Count());
        return entries.ToDictionary(entry => entry.Name!, entry => entry.Value ?? string.Empty);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repo root (agenda-buddy.sln) walking up from {AppContext.BaseDirectory}.");
    }
}