using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// Deleting an account spans two databases, and the order it happens in is the whole design.
/// </summary>
/// <remarks>
/// The Identity credential authorises the domain delete. So the domain profile goes first: a failure at either
/// step then leaves an account the user can still sign into and delete again. The reverse order would leave a live
/// profile — carrying their name, address and phone — that nothing could reach to finish erasing, and no way to
/// sign back in and retry.
/// </remarks>
public class ProfileDeleteAccountTests
{
    private const string Email = "ada@example.com";

    private static ProfileViewModel Build(
        bool isProvider,
        out Mock<IProviderApiService> provider,
        out Mock<ICustomerApiService> customer,
        out Mock<IAuthService> auth,
        out List<string> order,
        bool profileDeleteSucceeds = true,
        bool credentialDeleteSucceeds = true,
        bool profileDeleteThrows = false)
    {
        var calls = new List<string>();
        order = calls;

        provider = new Mock<IProviderApiService>();
        customer = new Mock<ICustomerApiService>();
        auth = new Mock<IAuthService>();

        if (profileDeleteThrows)
        {
            provider.Setup(p => p.DeleteAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("offline"));
            customer.Setup(c => c.DeleteAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("offline"));
        }
        else
        {
            provider.Setup(p => p.DeleteAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(profileDeleteSucceeds)
                    .Callback(() => calls.Add("profile"));
            customer.Setup(c => c.DeleteAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(profileDeleteSucceeds)
                    .Callback(() => calls.Add("profile"));
        }

        auth.Setup(a => a.DeleteAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentialDeleteSucceeds)
            .Callback(() => calls.Add("credential"));

        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(Email);
        session.SetupGet(s => s.Role).Returns(isProvider ? "Provider" : "Customer");
        session.SetupGet(s => s.IsProvider).Returns(isProvider);
        session.SetupGet(s => s.IsCustomer).Returns(!isProvider);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);

        return new ProfileViewModel(provider.Object, customer.Object, auth.Object, session.Object)
        {
            Email = Email
        };
    }

    /// <summary>⚠️ The ordering guarantee. Profile first, credential second, never the other way round.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheProfileIsDeletedBeforeTheCredential(bool isProvider)
    {
        var vm = Build(isProvider, out _, out _, out _, out var order);

        await vm.DeleteAccountCommand.ExecuteAsync(null);

        Assert.Equal(["profile", "credential"], order);
    }

    /// <summary>Each role's own route, so a deletion never asks the wrong service to erase the account.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheCallersOwnRoleRouteIsUsed(bool isProvider)
    {
        var vm = Build(isProvider, out var provider, out var customer, out _, out _);

        await vm.DeleteAccountCommand.ExecuteAsync(null);

        if (isProvider)
        {
            provider.Verify(p => p.DeleteAccountAsync(Email, It.IsAny<CancellationToken>()), Times.Once);
            customer.Verify(c => c.DeleteAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            customer.Verify(c => c.DeleteAccountAsync(Email, It.IsAny<CancellationToken>()), Times.Once);
            provider.Verify(p => p.DeleteAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    /// <summary>
    /// ⚠️ <b>Deleting is not deactivating.</b> The provider path must reach the erasure route, never the
    /// <c>IsActive=false</c> one — a "delete" that only hid the provider would keep every name, address and phone
    /// number on file while telling the user they were gone.
    /// </summary>
    [Fact]
    public async Task DeletingAProviderAccountNeverFallsBackToDeactivation()
    {
        var vm = Build(true, out var provider, out _, out _, out _);

        await vm.DeleteAccountCommand.ExecuteAsync(null);

        provider.Verify(p => p.DeactivateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// ⚠️ <b>A failed profile delete stops the whole thing.</b> Deleting the credential anyway to make the screen
    /// look like it worked would lock the user out of an account whose data is still stored — the one outcome
    /// worse than a failed delete.
    /// </summary>
    [Fact]
    public async Task WhenTheProfileDeleteFails_TheCredentialIsLeftAlone()
    {
        var vm = Build(false, out _, out _, out var auth, out _, profileDeleteSucceeds: false);
        var deleted = false;
        vm.AccountDeleted += (_, _) => deleted = true;

        await vm.DeleteAccountCommand.ExecuteAsync(null);

        auth.Verify(a => a.DeleteAccountAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(deleted);
        Assert.True(vm.HasError);
        Assert.Contains("Nothing has been removed", vm.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreachableServerIsReportedAndTheCredentialIsLeftAlone()
    {
        var vm = Build(false, out _, out _, out var auth, out _, profileDeleteThrows: true);

        await vm.DeleteAccountCommand.ExecuteAsync(null);

        auth.Verify(a => a.DeleteAccountAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(vm.HasError);
    }

    /// <summary>
    /// The profile is gone, so the account is unusable either way — but a credential left behind is a sign-in that
    /// reaches nothing, and only the user can tell us it happened. The screen still leaves for login.
    /// </summary>
    [Fact]
    public async Task WhenTheCredentialDeleteFailsAfterTheProfileIsGone_ItStillFinishesAndSignsOut()
    {
        var vm = Build(false, out _, out _, out _, out var order, credentialDeleteSucceeds: false);
        var deleted = false;
        vm.AccountDeleted += (_, _) => deleted = true;

        await vm.DeleteAccountCommand.ExecuteAsync(null);

        Assert.Equal(["profile", "credential"], order);
        Assert.True(deleted);
    }

    [Fact]
    public async Task ASuccessfulDeletionRaisesTheEventThatLeavesForLogin()
    {
        var vm = Build(false, out _, out _, out _, out _);
        var deleted = false;
        vm.AccountDeleted += (_, _) => deleted = true;

        await vm.DeleteAccountCommand.ExecuteAsync(null);

        Assert.True(deleted);
        Assert.False(vm.HasError);
    }

    /// <summary>The busy flag is released on every path, or the button stays hidden behind a spinner forever.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task TheBusyFlagIsAlwaysReleased(bool profileSucceeds, bool credentialSucceeds)
    {
        var vm = Build(false, out _, out _, out _, out _,
            profileDeleteSucceeds: profileSucceeds, credentialDeleteSucceeds: credentialSucceeds);

        await vm.DeleteAccountCommand.ExecuteAsync(null);

        Assert.False(vm.IsDeleting);
    }
}
