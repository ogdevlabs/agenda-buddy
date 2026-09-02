using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// A provider's availability window is generated in THEIR timezone, so the server has to know it. It is
/// taken from the device rather than asked for.
/// </summary>
public class AccountTimeZoneSyncTests
{
    private const string ProviderEmail = "coach@example.com";
    private const string CustomerEmail = "me@example.com";

    private static Mock<IUserSessionService> Session(string role)
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(role == "Provider" ? ProviderEmail : CustomerEmail);
        session.SetupGet(s => s.Role).Returns(role);
        session.SetupGet(s => s.IsProvider).Returns(role == "Provider");
        session.SetupGet(s => s.IsCustomer).Returns(role == "Customer");
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session;
    }

    private static (AccountViewModel Vm, Mock<IProviderApiService> ProviderApi) Build(
        string role, Mock<IProviderApiService>? providerApi = null)
    {
        providerApi ??= new Mock<IProviderApiService>();
        providerApi.Setup(p => p.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ProfileInfo { Email = ProviderEmail, FirstName = "Pat", LastName = "Coach" });

        var customerApi = new Mock<ICustomerApiService>();
        customerApi.Setup(c => c.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ProfileInfo { Email = CustomerEmail, FirstName = "Me", LastName = "Too" });

        return (new AccountViewModel(
            providerApi.Object, customerApi.Object, Mock.Of<IAuthService>(), Session(role).Object), providerApi);
    }

    [Fact]
    public async Task AProviderReportsItsDeviceTimeZoneOnLoad()
    {
        var (vm, providerApi) = Build("Provider");

        await vm.LoadCommand.ExecuteAsync(null);

        providerApi.Verify(p => p.SyncTimeZoneAsync(ProviderEmail, It.IsAny<CancellationToken>()), Times.Once);
    }

    // A customer has no working hours, so there is nothing to record and no reason to write.
    [Fact]
    public async Task ACustomerDoesNotReportATimeZone()
    {
        var (vm, providerApi) = Build("Customer");

        await vm.LoadCommand.ExecuteAsync(null);

        providerApi.Verify(p => p.SyncTimeZoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // It is a background correction: failing to record the zone must not break the profile screen or
    // surface an error the user cannot act on.
    [Fact]
    public async Task AFailedSyncIsSilentAndLeavesTheProfileUsable()
    {
        var providerApi = new Mock<IProviderApiService>();
        providerApi.Setup(p => p.SyncTimeZoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new HttpRequestException("offline"));
        var (vm, _) = Build("Provider", providerApi);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.Equal("Pat", vm.FirstName);
    }
}
