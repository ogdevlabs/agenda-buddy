using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class ShowcaseNavigationTests
{
    [Fact]
    public void ParametersCarryTheRefTheSourceAndTheEmail()
    {
        var parameters = ShowcaseNavigation.Parameters("ref-1", "pat@example.com", ShowcaseSource.Directory);

        Assert.Equal("ref-1", parameters["providerRef"]);
        Assert.Equal(ShowcaseSource.Directory, parameters["source"]);
        Assert.Equal("pat@example.com", parameters["email"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ABlankEmailIsOmittedRatherThanSentEmpty(string? email)
    {
        var parameters = ShowcaseNavigation.Parameters("ref-1", email, ShowcaseSource.Scan);

        Assert.False(parameters.ContainsKey("email"));
    }

    [Fact]
    public void TheRouteIsTheRegisteredShowcaseRoute() => Assert.Equal("providerShowcase", ShowcaseNavigation.Route);
}

public class ShowcaseLinkOpenTests
{
    private static ShowcaseLinkViewModel Build(Mock<IShowcaseApiService> api) => new(api.Object);

    [Fact]
    public void OpenDoesNothingWithoutAShowcase()
    {
        var vm = Build(new Mock<IShowcaseApiService>());
        var raised = false;
        vm.ShowcaseRequested += (_, _) => raised = true;

        vm.OpenCommand.Execute(null);

        Assert.False(raised);
    }

    [Fact]
    public void OpenRaisesWithTheRefEmailAndSource()
    {
        var vm = Build(new Mock<IShowcaseApiService>());
        vm.Source = ShowcaseSource.Message;
        vm.Preset("pat@example.com", "Pat Coach", "ref-1", "photo");
        ShowcaseRequestedEventArgs? args = null;
        vm.ShowcaseRequested += (_, e) => args = e;

        vm.OpenCommand.Execute(null);

        Assert.NotNull(args);
        Assert.Equal("ref-1", args.ProviderRef);
        Assert.Equal("pat@example.com", args.Email);
        Assert.Equal(ShowcaseSource.Message, args.Source);
        Assert.True(vm.HasPhoto);
    }

    [Fact]
    public async Task ASecondLoadForTheSameAddressDoesNotLookUpAgain()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.LookupAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(new List<ShowcaseLookupEntry>
            {
                new() { Email = "pat@example.com", ProviderRef = "ref-1" }
            }));
        var vm = Build(api);

        await vm.LoadAsync("pat@example.com", "Pat");
        await vm.LoadAsync("pat@example.com", "Pat");

        api.Verify(a => a.LookupAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ALookupFailureLeavesNoLinkAndDoesNotThrow()
    {
        var api = new Mock<IShowcaseApiService>();
        api.Setup(a => a.LookupAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        var vm = Build(api);

        await vm.LoadAsync("pat@example.com", "Pat");

        Assert.False(vm.HasShowcase);
    }

    [Fact]
    public async Task ABlankAddressIsNotLookedUp()
    {
        var api = new Mock<IShowcaseApiService>();
        var vm = Build(api);

        await vm.LoadAsync(" ", "Pat");

        api.Verify(a => a.LookupAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class DirectoryShowcaseLinkTests
{
    private static CustomersViewModel Build()
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns("me@example.com");
        session.SetupGet(s => s.IsCustomer).Returns(true);
        return new CustomersViewModel(
            new Mock<ICustomerApiService>().Object, new Mock<IProviderApiService>().Object, session.Object);
    }

    [Fact]
    public void AProviderRowWithAShowcaseOpensItFromTheDirectory()
    {
        var vm = Build();
        ShowcaseRequestedEventArgs? args = null;
        vm.ShowcaseRequested += (_, e) => args = e;

        vm.OpenShowcaseCommand.Execute(new CustomerSummary { IsProvider = true, ProviderRef = "ref-1", Email = "pat@example.com" });

        Assert.NotNull(args);
        Assert.Equal("ref-1", args.ProviderRef);
        Assert.Equal("pat@example.com", args.Email);
        Assert.Equal(ShowcaseSource.Directory, args.Source);
    }

    [Fact]
    public void ARowWithoutAShowcaseOffersNothing()
    {
        var vm = Build();
        var raised = false;
        vm.ShowcaseRequested += (_, _) => raised = true;

        vm.OpenShowcaseCommand.Execute(new CustomerSummary { IsProvider = true, Email = "pat@example.com" });
        vm.OpenShowcaseCommand.Execute(new CustomerSummary { IsProvider = false, ProviderRef = "ref-1" });
        vm.OpenShowcaseCommand.Execute(null);

        Assert.False(raised);
    }
}

public class DashboardFindProviderCardTests
{
    private static Mock<IUserSessionService> Session(string role)
    {
        var session = new Mock<IUserSessionService>();
        session.Setup(s => s.Email).Returns("me@example.com");
        session.Setup(s => s.Role).Returns(role);
        session.Setup(s => s.IsProvider).Returns(role == "Provider");
        session.Setup(s => s.IsCustomer).Returns(role == "Customer");
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session;
    }

    private static DashboardViewModel Build(
        string role, List<AppointmentSummary> upcoming, Mock<ICustomerApiService> customerApi)
    {
        var booking = new Mock<IBookingApiService>();
        booking.Setup(b => b.GetUpcomingAppointmentsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(upcoming);
        var session = Session(role);
        var header = new BrandHeaderViewModel(session.Object, new Mock<IProviderApiService>().Object,
            new Mock<ICustomerApiService>().Object, new NotificationBadgeViewModel(new Mock<INotificationApiService>().Object));
        return new DashboardViewModel(booking.Object, session.Object, header, customerApi.Object);
    }

    private static Mock<ICustomerApiService> Subscriptions(params string[] providers)
    {
        var api = new Mock<ICustomerApiService>();
        api.Setup(c => c.GetSubscriptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(providers.ToList());
        return api;
    }

    [Fact]
    public async Task ACustomerWithNoProviderIsOfferedTheScanCard()
    {
        var vm = Build("Customer", [], Subscriptions());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.ShowFindProviderCard);
    }

    [Fact]
    public async Task ASubscriptionMeansTheCustomerAlreadyHasAProvider()
    {
        var vm = Build("Customer", [], Subscriptions("pat@example.com"));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.ShowFindProviderCard);
    }

    [Fact]
    public async Task AnUpcomingAppointmentMeansTheCustomerAlreadyHasAProvider()
    {
        var api = Subscriptions();
        var vm = Build("Customer", [new AppointmentSummary { Id = "a1" }], api);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.ShowFindProviderCard);
        api.Verify(c => c.GetSubscriptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AProviderNeverSeesTheScanCard()
    {
        var vm = Build("Provider", [], Subscriptions());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.ShowFindProviderCard);
    }

    /// <summary>Unknown is treated as "has one": a card telling somebody with providers to go and find one is wrong.</summary>
    [Fact]
    public async Task AFailedSubscriptionReadKeepsTheCardHidden()
    {
        var api = new Mock<ICustomerApiService>();
        api.Setup(c => c.GetSubscriptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        var vm = Build("Customer", [], api);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.ShowFindProviderCard);
    }

    [Fact]
    public void TheCardAsksToOpenTheScanner()
    {
        var vm = Build("Customer", [], Subscriptions());
        var raised = false;
        vm.ScanProviderRequested += (_, _) => raised = true;

        vm.ScanProviderCommand.Execute(null);

        Assert.True(raised);
    }
}

public class ProfileShowcasePhotoTests
{
    private static ProfileViewModel Build(string role, Mock<IShowcaseApiService> showcase)
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns("me@example.com");
        session.SetupGet(s => s.Role).Returns(role);
        session.SetupGet(s => s.IsProvider).Returns(role == "Provider");
        session.SetupGet(s => s.IsCustomer).Returns(role == "Customer");
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);

        var providerApi = new Mock<IProviderApiService>();
        providerApi.Setup(p => p.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileInfo { Email = "me@example.com", FirstName = "Pat", LastName = "Coach" });
        var customerApi = new Mock<ICustomerApiService>();
        customerApi.Setup(c => c.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileInfo { Email = "me@example.com", FirstName = "Me", LastName = "Too" });

        return new ProfileViewModel(providerApi.Object, customerApi.Object, Mock.Of<IAuthService>(), session.Object,
            showcaseApi: showcase.Object);
    }

    [Fact]
    public async Task AProviderHeroShowsTheShowcasePhoto()
    {
        var showcase = new Mock<IShowcaseApiService>();
        showcase.Setup(s => s.GetMineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ShowcaseFixtures.Ok(ShowcaseFixtures.Mine(photo: "p1")));
        var vm = Build("Provider", showcase);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("ref-1", vm.ShowcaseProviderRef);
        Assert.Equal("p1", vm.ShowcasePhotoHash);
    }

    [Fact]
    public async Task ACustomerNeverAsksForAShowcase()
    {
        var showcase = new Mock<IShowcaseApiService>();
        var vm = Build("Customer", showcase);

        await vm.LoadCommand.ExecuteAsync(null);

        showcase.Verify(s => s.GetMineAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AFailedShowcaseReadLeavesTheProfileUsable()
    {
        var showcase = new Mock<IShowcaseApiService>();
        showcase.Setup(s => s.GetMineAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));
        var vm = Build("Provider", showcase);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(string.IsNullOrEmpty(vm.ShowcasePhotoHash));
        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage));
    }
}
