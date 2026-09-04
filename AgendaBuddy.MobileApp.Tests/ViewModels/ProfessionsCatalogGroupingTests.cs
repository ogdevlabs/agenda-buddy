using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// The catalog is ~120 professions. It is rendered as collapsible letter sections, closed by default, so
/// the page opens on about two dozen lines instead of a wall of names.
/// </summary>
public class ProfessionsCatalogGroupingTests
{
    private static IUserSessionService Session(bool isProvider)
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns("pat@test.dev");
        session.SetupGet(s => s.IsProvider).Returns(isProvider);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session.Object;
    }

    private static async Task<ProfessionsViewModel> LoadedAsync(bool isProvider, params string[] names)
    {
        var api = new Mock<IProfessionApiService>();
        api.Setup(a => a.GetProfessionsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(names.Select(n => new ProfessionItem { Name = n }).ToList());
        api.Setup(a => a.GetProviderProfessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync([]);

        var vm = new ProfessionsViewModel(api.Object, Session(isProvider));
        await vm.LoadCommand.ExecuteAsync(null);
        return vm;
    }

    private static List<string> Headers(ProfessionsViewModel vm) =>
        vm.CatalogRows.Where(r => r.IsHeader).Select(r => r.Letter).ToList();

    private static List<string> VisibleProfessions(ProfessionsViewModel vm) =>
        vm.CatalogRows.Where(r => r.IsProfession).Select(r => r.Profession!.Name).ToList();

    [Fact]
    public async Task EverySectionStartsClosedSoOnlyHeadersAreListed()
    {
        var vm = await LoadedAsync(true, "Accounting", "Art", "Boxing", "Coaching");

        Assert.Equal(["A", "B", "C"], Headers(vm));
        Assert.Empty(VisibleProfessions(vm));
        Assert.All(vm.CatalogRows, r => Assert.False(r.IsExpanded));
    }

    [Fact]
    public async Task AHeaderCountsItsProfessionsWhileStillClosed()
    {
        var vm = await LoadedAsync(true, "Accounting", "Art", "Boxing");

        var a = vm.CatalogRows.Single(r => r.IsHeader && r.Letter == "A");
        Assert.Equal(2, a.MemberCount);
        Assert.Equal(1, vm.CatalogRows.Single(r => r.IsHeader && r.Letter == "B").MemberCount);
    }

    [Fact]
    public async Task OpeningALetterRevealsOnlyThatLettersProfessions()
    {
        var vm = await LoadedAsync(true, "Accounting", "Art", "Boxing");

        vm.ToggleGroupCommand.Execute("A");

        Assert.Equal(["Accounting", "Art"], VisibleProfessions(vm));
        Assert.True(vm.CatalogRows.Single(r => r.IsHeader && r.Letter == "A").IsExpanded);
        Assert.False(vm.CatalogRows.Single(r => r.IsHeader && r.Letter == "B").IsExpanded);
    }

    [Fact]
    public async Task ClosingALetterAgainHidesItsProfessions()
    {
        var vm = await LoadedAsync(true, "Accounting", "Boxing");

        vm.ToggleGroupCommand.Execute("A");
        vm.ToggleGroupCommand.Execute("A");

        Assert.Empty(VisibleProfessions(vm));
    }

    [Fact]
    public async Task ProfessionsAreAlphabeticalWithinTheirLetterEvenWhenTheCatalogIsNot()
    {
        // The seeded catalog is not sorted: "Artificial Intelligence" ships before "Anti-Aging".
        var vm = await LoadedAsync(true, "Artificial Intelligence", "Accounting", "Anti-Aging");

        vm.ToggleGroupCommand.Execute("A");

        Assert.Equal(["Accounting", "Anti-Aging", "Artificial Intelligence"], VisibleProfessions(vm));
    }

    [Fact]
    public async Task HeadersAreInLetterOrder()
    {
        var vm = await LoadedAsync(true, "Yoga", "Art", "Nutrition");

        Assert.Equal(["A", "N", "Y"], Headers(vm));
    }

    [Fact]
    public async Task LettersWithNoProfessionsGetNoHeader()
    {
        var vm = await LoadedAsync(true, "Art", "Zoology");

        Assert.Equal(["A", "Z"], Headers(vm));
        Assert.DoesNotContain("B", Headers(vm));
    }

    [Fact]
    public async Task NamesNotStartingWithALetterShareOneBucket()
    {
        var vm = await LoadedAsync(true, "3D Printing", "#Hashtags", "Art");

        Assert.Equal(["#", "A"], Headers(vm));
        Assert.Equal(2, vm.CatalogRows.Single(r => r.IsHeader && r.Letter == "#").MemberCount);
    }

    [Fact]
    public async Task GroupingIsCaseInsensitiveOnTheFirstLetter()
    {
        var vm = await LoadedAsync(true, "art", "Archery");

        Assert.Equal(["A"], Headers(vm));
        Assert.Equal(2, vm.CatalogRows.Single(r => r.IsHeader).MemberCount);
    }

    [Fact]
    public async Task SearchingOpensEveryLetterThatHasAMatch()
    {
        var vm = await LoadedAsync(true, "Accounting", "Art", "Boxing", "Coaching");

        vm.SearchText = "ing";

        Assert.Equal(["A", "B", "C"], Headers(vm));
        Assert.Equal(["Accounting", "Boxing", "Coaching"], VisibleProfessions(vm));
        Assert.All(vm.CatalogRows.Where(r => r.IsHeader), r => Assert.True(r.IsExpanded));
    }

    [Fact]
    public async Task ALetterWithNoMatchDisappearsWhileSearching()
    {
        var vm = await LoadedAsync(true, "Accounting", "Boxing");

        vm.SearchText = "Account";

        Assert.Equal(["A"], Headers(vm));
        Assert.Equal(["Accounting"], VisibleProfessions(vm));
    }

    [Fact]
    public async Task ClearingTheSearchReturnsSectionsToWhatWasActuallyOpen()
    {
        var vm = await LoadedAsync(true, "Accounting", "Art", "Boxing");

        vm.ToggleGroupCommand.Execute("B");
        vm.SearchText = "a";
        vm.SearchText = string.Empty;

        Assert.Equal(["Boxing"], VisibleProfessions(vm));
    }

    [Fact]
    public async Task SearchDoesNotSilentlyOpenSectionsForGood()
    {
        var vm = await LoadedAsync(true, "Accounting", "Boxing");

        vm.SearchText = "ing";
        vm.SearchText = string.Empty;

        Assert.Empty(VisibleProfessions(vm));
    }

    [Fact]
    public async Task TogglingIsIgnoredWhileSearchingSinceSearchDecidesWhatIsOpen()
    {
        var vm = await LoadedAsync(true, "Accounting", "Art");

        vm.SearchText = "a";
        vm.ToggleGroupCommand.Execute("A");

        Assert.Equal(["Accounting", "Art"], VisibleProfessions(vm));

        vm.SearchText = string.Empty;
        Assert.Empty(VisibleProfessions(vm));
    }

    [Fact]
    public async Task SelectingInsideASectionSurvivesClosingIt()
    {
        var vm = await LoadedAsync(true, "Accounting", "Art");

        vm.ToggleGroupCommand.Execute("A");
        var accounting = vm.CatalogRows.Single(r => r.IsProfession && r.Profession!.Name == "Accounting").Profession!;
        vm.ToggleSelectCommand.Execute(accounting);

        vm.ToggleGroupCommand.Execute("A");
        vm.ToggleGroupCommand.Execute("A");

        Assert.True(vm.CatalogRows.Single(r => r.IsProfession && r.Profession!.Name == "Accounting").Profession!.IsSelected);
        Assert.True(vm.HasSelectionChanges);
    }

    [Fact]
    public async Task ACustomerSeesTheSameSectionsButCannotSelect()
    {
        var vm = await LoadedAsync(false, "Accounting", "Art");

        Assert.Equal(["A"], Headers(vm));

        vm.ToggleGroupCommand.Execute("A");
        var accounting = vm.CatalogRows.First(r => r.IsProfession).Profession!;
        vm.ToggleSelectCommand.Execute(accounting);

        Assert.False(accounting.IsSelected);
    }

    [Fact]
    public async Task AHeaderChevronReflectsWhetherItIsOpen()
    {
        var vm = await LoadedAsync(true, "Art");

        Assert.Equal("▸", vm.CatalogRows.Single(r => r.IsHeader).Chevron);

        vm.ToggleGroupCommand.Execute("A");

        Assert.Equal("▾", vm.CatalogRows.Single(r => r.IsHeader).Chevron);
    }

    [Fact]
    public async Task AnEmptyCatalogProducesNoRowsAtAll()
    {
        var vm = await LoadedAsync(true);

        Assert.Empty(vm.CatalogRows);
        Assert.True(vm.IsEmpty);
    }
}
