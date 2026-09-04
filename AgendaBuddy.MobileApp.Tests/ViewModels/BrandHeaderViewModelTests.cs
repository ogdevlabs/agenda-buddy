using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// The header decorates every page, so it has to render something for every session state — including the
/// ones where no profile exists and the ones where the network is gone.
/// </summary>
public class BrandHeaderViewModelTests
{
    private const string ProviderEmail = "coach@example.com";
    private const string CustomerEmail = "client@example.com";

    private static Mock<IUserSessionService> Session(string email, string role)
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(email);
        session.SetupGet(s => s.Role).Returns(role);
        session.SetupGet(s => s.IsProvider).Returns(string.Equals(role, "provider", StringComparison.OrdinalIgnoreCase));
        session.SetupGet(s => s.IsCustomer).Returns(string.Equals(role, "customer", StringComparison.OrdinalIgnoreCase));
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session;
    }

    private static BrandHeaderViewModel Build(
        Mock<IUserSessionService> session,
        Mock<IProviderApiService>? providerApi = null,
        Mock<ICustomerApiService>? customerApi = null) =>
        new(session.Object,
            (providerApi ?? new Mock<IProviderApiService>()).Object,
            (customerApi ?? new Mock<ICustomerApiService>()).Object);

    [Fact]
    public async Task ProviderWithAProfileShowsTheirName()
    {
        var providerApi = new Mock<IProviderApiService>();
        providerApi.Setup(p => p.GetProfileAsync(ProviderEmail, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ProfileInfo { Email = ProviderEmail, FirstName = "Pat", LastName = "Coach" });

        var vm = Build(Session(ProviderEmail, "provider"), providerApi);

        await vm.RefreshAsync();

        Assert.Equal("Pat Coach", vm.DisplayName);
        Assert.Equal("Provider", vm.RoleLabel);
        Assert.True(vm.HasUser);
        Assert.True(vm.HasRole);
    }

    [Fact]
    public async Task CustomerNameComesFromTheCustomerProfileNotTheProviderOne()
    {
        var providerApi = new Mock<IProviderApiService>();
        var customerApi = new Mock<ICustomerApiService>();
        customerApi.Setup(c => c.GetProfileAsync(CustomerEmail, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ProfileInfo { Email = CustomerEmail, FirstName = "Sam", LastName = "Client" });

        var vm = Build(Session(CustomerEmail, "customer"), providerApi, customerApi);

        await vm.RefreshAsync();

        Assert.Equal("Sam Client", vm.DisplayName);
        Assert.Equal("Customer", vm.RoleLabel);
        providerApi.Verify(p => p.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BlankNameOnTheProfileFallsBackToTheEmail()
    {
        var providerApi = new Mock<IProviderApiService>();
        providerApi.Setup(p => p.GetProfileAsync(ProviderEmail, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ProfileInfo { Email = ProviderEmail, FirstName = "  ", LastName = "" });

        var vm = Build(Session(ProviderEmail, "provider"), providerApi);

        await vm.RefreshAsync();

        Assert.Equal(ProviderEmail, vm.DisplayName);
    }

    [Fact]
    public async Task MissingProfileFallsBackToTheEmail()
    {
        var providerApi = new Mock<IProviderApiService>();
        providerApi.Setup(p => p.GetProfileAsync(ProviderEmail, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((ProfileInfo?)null);

        var vm = Build(Session(ProviderEmail, "provider"), providerApi);

        await vm.RefreshAsync();

        Assert.Equal(ProviderEmail, vm.DisplayName);
    }

    [Fact]
    public async Task ProfileFetchThrowingFallsBackToTheEmailInsteadOfPropagating()
    {
        var providerApi = new Mock<IProviderApiService>();
        providerApi.Setup(p => p.GetProfileAsync(ProviderEmail, It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new HttpRequestException("gateway down"));

        var vm = Build(Session(ProviderEmail, "provider"), providerApi);

        await vm.RefreshAsync();

        Assert.Equal(ProviderEmail, vm.DisplayName);
    }

    [Fact]
    public async Task AFailedFetchIsRetriedOnTheNextRefresh()
    {
        var providerApi = new Mock<IProviderApiService>();
        providerApi.SetupSequence(p => p.GetProfileAsync(ProviderEmail, It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new HttpRequestException("gateway down"))
                   .ReturnsAsync(new ProfileInfo { Email = ProviderEmail, FirstName = "Pat", LastName = "Coach" });

        var vm = Build(Session(ProviderEmail, "provider"), providerApi);

        await vm.RefreshAsync();
        Assert.Equal(ProviderEmail, vm.DisplayName);

        await vm.RefreshAsync();
        Assert.Equal("Pat Coach", vm.DisplayName);
    }

    [Fact]
    public async Task TheNameIsFetchedOncePerAccountNotOncePerRefresh()
    {
        var providerApi = new Mock<IProviderApiService>();
        providerApi.Setup(p => p.GetProfileAsync(ProviderEmail, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ProfileInfo { Email = ProviderEmail, FirstName = "Pat", LastName = "Coach" });

        var vm = Build(Session(ProviderEmail, "provider"), providerApi);

        await vm.RefreshAsync();
        await vm.RefreshAsync();
        await vm.RefreshAsync();

        providerApi.Verify(p => p.GetProfileAsync(ProviderEmail, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignedOutShowsNoUserLineAtAll()
    {
        var vm = Build(Session(string.Empty, string.Empty));

        await vm.RefreshAsync();

        Assert.Equal(string.Empty, vm.DisplayName);
        Assert.Equal(string.Empty, vm.RoleLabel);
        Assert.False(vm.HasUser);
        Assert.False(vm.HasRole);
    }

    [Fact]
    public async Task SigningOutClearsAPreviouslyResolvedName()
    {
        var session = Session(ProviderEmail, "provider");
        var providerApi = new Mock<IProviderApiService>();
        providerApi.Setup(p => p.GetProfileAsync(ProviderEmail, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ProfileInfo { Email = ProviderEmail, FirstName = "Pat", LastName = "Coach" });

        var vm = Build(session, providerApi);
        await vm.RefreshAsync();
        Assert.Equal("Pat Coach", vm.DisplayName);

        session.SetupGet(s => s.Email).Returns(string.Empty);
        session.SetupGet(s => s.Role).Returns(string.Empty);
        await vm.RefreshAsync();

        Assert.False(vm.HasUser);
    }

    [Fact]
    public async Task SwitchingAccountResolvesTheNewNameRatherThanKeepingTheOld()
    {
        var session = Session(ProviderEmail, "provider");
        var providerApi = new Mock<IProviderApiService>();
        providerApi.Setup(p => p.GetProfileAsync(ProviderEmail, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ProfileInfo { Email = ProviderEmail, FirstName = "Pat", LastName = "Coach" });
        providerApi.Setup(p => p.GetProfileAsync("other@example.com", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ProfileInfo { Email = "other@example.com", FirstName = "Dana", LastName = "Tutor" });

        var vm = Build(session, providerApi);
        await vm.RefreshAsync();
        Assert.Equal("Pat Coach", vm.DisplayName);

        session.SetupGet(s => s.Email).Returns("other@example.com");
        await vm.RefreshAsync();

        Assert.Equal("Dana Tutor", vm.DisplayName);
    }

    [Fact]
    public async Task AnUnrecognisedRoleShowsTheNameWithNoRoleChip()
    {
        var vm = Build(Session(ProviderEmail, "administrator"));

        await vm.RefreshAsync();

        Assert.Equal(ProviderEmail, vm.DisplayName);
        Assert.Equal(string.Empty, vm.RoleLabel);
        Assert.False(vm.HasRole);
    }

    [Fact]
    public async Task AnUndecodableTokenIsTreatedAsSignedOutRatherThanThrowing()
    {
        var session = Session(ProviderEmail, "provider");
        session.Setup(s => s.RefreshAsync()).ThrowsAsync(new FormatException("not base64"));

        var vm = Build(session);

        await vm.RefreshAsync();

        Assert.False(vm.HasUser);
    }
}
