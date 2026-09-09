using System.Globalization;
using System.Xml.Linq;
using AgendaBuddy.Library.Data;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

[Collection(nameof(CultureSensitiveCollection))]
public class ProfessionDisplayNamesTests
{
    private const string Email = "provider@example.com";

    [Fact]
    public void EverySeedHasExactlyOneMappingAndThereAreNoOrphans()
    {
        var seedNames = ProfessionSeedData.SeedData().Select(profession => profession.Name).ToList();

        Assert.Equal(115, seedNames.Count);
        Assert.Equal(seedNames.Count, seedNames.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(seedNames.Order(StringComparer.Ordinal), ProfessionDisplayNames.ResourceKeys.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(seedNames.Count, ProfessionDisplayNames.ResourceKeys.Values.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es-MX")]
    public void EverySeedResolvesToANonEmptyDisplayNameInEverySupportedCulture(string cultureName)
    {
        WithCulture(cultureName, () =>
        {
            foreach (var profession in ProfessionSeedData.SeedData())
                Assert.False(string.IsNullOrWhiteSpace(ProfessionDisplayNames.Get(profession.Name)));
        });
    }

    [Fact]
    public void ProfessionResourcesContainExactlyTheMappedKeys()
    {
        var mappedKeys = ProfessionDisplayNames.ResourceKeys.Values.Order(StringComparer.Ordinal).ToList();

        Assert.Equal(mappedKeys, ProfessionResourceKeys("AppResources.resx"));
        Assert.Equal(mappedKeys, ProfessionResourceKeys("AppResources.es-MX.resx"));
    }

    [Fact]
    public void UnknownCanonicalNameFallsBackWithoutChangingIt()
    {
        WithCulture("es-MX", () => Assert.Equal("Future profession", ProfessionDisplayNames.Get("Future profession")));
    }

    [Fact]
    public async Task SpanishSearchAndGroupingUseLocalizedDisplayNames()
    {
        await WithCultureAsync("es-MX", async () =>
        {
            var api = ProfessionApi([new ProfessionItem { Name = "Accounting" }, new ProfessionItem { Name = "Art" }], []);
            var viewModel = new ProfessionsViewModel(api.Object, Session());

            await viewModel.LoadCommand.ExecuteAsync(null);
            viewModel.SearchText = "conta";

            var row = Assert.Single(viewModel.CatalogRows, item => item.IsProfession);
            Assert.Equal("Accounting", row.Profession!.Name);
            Assert.Equal("Contabilidad", row.Profession.DisplayName);
            Assert.Equal("C", Assert.Single(viewModel.CatalogRows, item => item.IsHeader).Letter);
        });
    }

    [Fact]
    public async Task ProfessionAddAndRemoveWritesKeepCanonicalNames()
    {
        await WithCultureAsync("es-MX", async () =>
        {
            var api = ProfessionApi([new ProfessionItem { Name = "Accounting" }], []);
            List<string>? added = null;
            string? removed = null;
            api.Setup(service => service.AddProfessionsToProviderAsync(Email, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .Callback<string, List<string>, CancellationToken>((_, names, _) => added = names)
                .ReturnsAsync(true);
            api.Setup(service => service.RemoveProfessionFromProviderAsync(Email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((_, name, _) => removed = name)
                .ReturnsAsync(new ProfessionRemovalResult(true, null));
            var viewModel = new ProfessionsViewModel(api.Object, Session());

            await viewModel.LoadCommand.ExecuteAsync(null);
            viewModel.ToggleSelectCommand.Execute(viewModel.FilteredCatalog.Single());
            await viewModel.SaveSelectionCommand.ExecuteAsync(null);
            await viewModel.RemoveCurrentCommand.ExecuteAsync("Accounting");

            Assert.Equal(["Accounting"], added);
            Assert.Equal("Accounting", removed);
        });
    }

    [Fact]
    public async Task ServicePickerDisplaysSpanishButWritesCanonicalProfessionName()
    {
        await WithCultureAsync("es-MX", async () =>
        {
            var professionApi = ProfessionApi([], ["Accounting"]);
            var servicesApi = new Mock<IServicesApiService>();
            List<ServiceItem>? sent = null;
            servicesApi.Setup(service => service.AddServicesAsync(Email, It.IsAny<List<ServiceItem>>(), It.IsAny<CancellationToken>()))
                .Callback<string, List<ServiceItem>, CancellationToken>((_, items, _) => sent = items)
                .ReturnsAsync(true);
            var viewModel = new AddServiceViewModel(servicesApi.Object, professionApi.Object, Session());

            await viewModel.LoadCommand.ExecuteAsync(null);
            Assert.Equal("Contabilidad", Assert.Single(viewModel.AvailableProfessionItems).DisplayName);
            Assert.Equal("Accounting", viewModel.ProfessionName);

            viewModel.ServiceName = "Consultation";
            viewModel.Description = "Advice";
            await viewModel.AddServiceCommand.ExecuteAsync(null);

            Assert.Equal("Accounting", Assert.Single(sent!).ProfessionName);
            Assert.Equal("Contabilidad", new ServiceItem { ProfessionName = "Accounting" }.ProfessionDisplayName);
        });
    }

    private static Mock<IProfessionApiService> ProfessionApi(List<ProfessionItem> catalog, List<string> current)
    {
        var api = new Mock<IProfessionApiService>();
        api.Setup(service => service.GetProfessionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(catalog);
        api.Setup(service => service.GetProviderProfessionsAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(current);
        return api;
    }

    private static IUserSessionService Session()
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(service => service.Email).Returns(Email);
        session.SetupGet(service => service.IsProvider).Returns(true);
        session.Setup(service => service.RefreshAsync()).Returns(Task.CompletedTask);
        return session.Object;
    }

    private static List<string> ProfessionResourceKeys(string fileName)
    {
        var path = Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "Resources", "Strings", fileName);
        return XDocument.Load(path)
            .Root!
            .Elements("data")
            .Select(element => (string?)element.Attribute("name"))
            .Where(name => name?.StartsWith("Profession_", StringComparison.Ordinal) == true)
            .Select(name => name!)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static void WithCulture(string cultureName, Action assertion)
    {
        var original = AppResources.Culture;
        try
        {
            AppResources.Culture = new CultureInfo(cultureName);
            assertion();
        }
        finally
        {
            AppResources.Culture = original;
        }
    }

    private static async Task WithCultureAsync(string cultureName, Func<Task> assertion)
    {
        var original = AppResources.Culture;
        try
        {
            AppResources.Culture = new CultureInfo(cultureName);
            await assertion();
        }
        finally
        {
            AppResources.Culture = original;
        }
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

        throw new DirectoryNotFoundException("Could not locate agenda-buddy.sln.");
    }
}