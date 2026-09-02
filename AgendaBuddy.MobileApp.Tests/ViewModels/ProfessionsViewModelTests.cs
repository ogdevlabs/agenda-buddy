using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class ProfessionsViewModelTests
{
    private static IUserSessionService CreateSession(bool isProvider, string email = "pat@test.dev")
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(email);
        session.SetupGet(s => s.IsProvider).Returns(isProvider);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session.Object;
    }

    private static Mock<IProfessionApiService> CreateApi(List<ProfessionItem> catalog, List<string> currentProfessions)
    {
        var api = new Mock<IProfessionApiService>();
        api.Setup(a => a.GetProfessionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(catalog);
        api.Setup(a => a.GetProviderProfessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(currentProfessions);
        return api;
    }

    [Fact]
    public async Task LoadAsync_Provider_MarksCatalogItemsSelectedFromCurrentProfessions()
    {
        var catalog = new List<ProfessionItem> { new() { Name = "Coaching" }, new() { Name = "Tutoring" } };
        var api = CreateApi(catalog, ["Coaching"]);
        var vm = new ProfessionsViewModel(api.Object, CreateSession(isProvider: true));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.FilteredCatalog.Single(p => p.Name == "Coaching").IsSelected);
        Assert.False(vm.FilteredCatalog.Single(p => p.Name == "Tutoring").IsSelected);
        Assert.Equal(["Coaching"], vm.CurrentProfessions);
    }

    [Fact]
    public async Task LoadAsync_Customer_DoesNotFetchProviderProfessions()
    {
        var catalog = new List<ProfessionItem> { new() { Name = "Coaching" } };
        var api = CreateApi(catalog, []);
        var vm = new ProfessionsViewModel(api.Object, CreateSession(isProvider: false));

        await vm.LoadCommand.ExecuteAsync(null);

        api.Verify(a => a.GetProviderProfessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(vm.CurrentProfessions);
    }

    [Fact]
    public async Task SearchText_FiltersCatalogCaseInsensitively()
    {
        var catalog = new List<ProfessionItem> { new() { Name = "Coaching" }, new() { Name = "Tutoring" } };
        var api = CreateApi(catalog, []);
        var vm = new ProfessionsViewModel(api.Object, CreateSession(isProvider: true));
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SearchText = "coach";

        Assert.Single(vm.FilteredCatalog);
        Assert.Equal("Coaching", vm.FilteredCatalog[0].Name);
    }

    [Fact]
    public async Task ToggleSelect_EnablesSaveSelectionCommand()
    {
        var catalog = new List<ProfessionItem> { new() { Name = "Coaching" } };
        var api = CreateApi(catalog, []);
        var vm = new ProfessionsViewModel(api.Object, CreateSession(isProvider: true));
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.SaveSelectionCommand.CanExecute(null));

        vm.ToggleSelectCommand.Execute(vm.FilteredCatalog[0]);

        Assert.True(vm.HasSelectionChanges);
        Assert.True(vm.SaveSelectionCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveSelectionAsync_Success_SendsOnlyNewlySelectedNames()
    {
        var catalog = new List<ProfessionItem> { new() { Name = "Coaching" }, new() { Name = "Tutoring" } };
        var api = CreateApi(catalog, ["Coaching"]);
        List<string>? sent = null;
        api.Setup(a => a.AddProfessionsToProviderAsync("pat@test.dev", It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
           .Callback<string, List<string>, CancellationToken>((_, names, _) => sent = names)
           .ReturnsAsync(true);
        var vm = new ProfessionsViewModel(api.Object, CreateSession(isProvider: true));
        await vm.LoadCommand.ExecuteAsync(null);
        vm.ToggleSelectCommand.Execute(vm.FilteredCatalog.Single(p => p.Name == "Tutoring"));

        await vm.SaveSelectionCommand.ExecuteAsync(null);

        Assert.Equal(["Tutoring"], sent);
        Assert.Contains("Tutoring", vm.CurrentProfessions);
        Assert.False(vm.HasSelectionChanges);
    }

    [Fact]
    public async Task RemoveCurrentAsync_Success_RemovesFromCurrentAndClearsSelection()
    {
        var catalog = new List<ProfessionItem> { new() { Name = "Coaching" } };
        var api = CreateApi(catalog, ["Coaching"]);
        api.Setup(a => a.RemoveProfessionFromProviderAsync("pat@test.dev", "Coaching", It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ProfessionRemovalResult(true, null));
        var vm = new ProfessionsViewModel(api.Object, CreateSession(isProvider: true));
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.RemoveCurrentCommand.ExecuteAsync("Coaching");

        Assert.DoesNotContain("Coaching", vm.CurrentProfessions);
        Assert.False(vm.FilteredCatalog.Single(p => p.Name == "Coaching").IsSelected);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task RemoveCurrentAsync_GuardFailure_SurfacesServerMessage()
    {
        var catalog = new List<ProfessionItem> { new() { Name = "Coaching" } };
        var api = CreateApi(catalog, ["Coaching"]);
        api.Setup(a => a.RemoveProfessionFromProviderAsync("pat@test.dev", "Coaching", It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ProfessionRemovalResult(false, "Cannot remove a profession while you have active appointments."));
        var vm = new ProfessionsViewModel(api.Object, CreateSession(isProvider: true));
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.RemoveCurrentCommand.ExecuteAsync("Coaching");

        Assert.True(vm.HasError);
        Assert.Equal("Cannot remove a profession while you have active appointments.", vm.ErrorMessage);
        Assert.Contains("Coaching", vm.CurrentProfessions);
    }
}
