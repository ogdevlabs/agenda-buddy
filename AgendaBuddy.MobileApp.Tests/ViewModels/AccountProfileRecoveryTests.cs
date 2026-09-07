using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// Account is the only screen that can repair a half-created account, and until it could <b>create</b> a
/// missing profile rather than only update an existing one, it could not.
/// </summary>
/// <remarks>
/// <para>
/// The hole: registration writes an Identity credential and then a domain profile. If the second call fails,
/// the user is signed in with a valid token, a correct role claim, and no profile — and is told to "add them
/// from Account to finish setting up". But <c>UpdateProfileAsync</c> reads the profile before writing and
/// gives up when the read 404s, and the route behind it answers <c>NotFound</c>, because
/// <c>FindOneAndUpdateAsync</c> never upserts (ADR-032 — deliberate, and load-bearing elsewhere: it is what
/// stops a failed login for an unknown address creating an account).
/// </para>
/// <para>
/// So every Save failed with "try again", and trying again could not help. The instruction was false, and the
/// account was unrecoverable from inside the app. Every account created before profile creation was wired at
/// all is in exactly that state.
/// </para>
/// </remarks>
public class AccountProfileRecoveryTests
{
    private const string Email = "stranded@example.com";

    private static AccountViewModel Create(
        bool isProvider,
        out Mock<IProviderApiService> provider,
        out Mock<ICustomerApiService> customer,
        bool updateSucceeds,
        bool createSucceeds = true)
    {
        provider = new Mock<IProviderApiService>();
        provider.Setup(p => p.UpdateProfileAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(updateSucceeds);
        provider.Setup(p => p.CreateProfileAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(createSucceeds);

        customer = new Mock<ICustomerApiService>();
        customer.Setup(c => c.UpdateProfileAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(updateSucceeds);
        customer.Setup(c => c.CreateProfileAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(createSucceeds);

        var session = new Mock<IUserSessionService>();
        session.Setup(s => s.Email).Returns(Email);
        session.Setup(s => s.Role).Returns(isProvider ? "Provider" : "Customer");
        session.Setup(s => s.IsProvider).Returns(isProvider);
        session.Setup(s => s.IsCustomer).Returns(!isProvider);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);

        var vm = new AccountViewModel(
            provider.Object, customer.Object, Mock.Of<IAuthService>(), session.Object);

        vm.Email = Email;
        vm.FirstName = "Ada";
        vm.LastName = "Lovelace";
        vm.PhoneNumber = "+15550100";
        // The editor is open, because that is the only state Save can be reached from. Without this the
        // assertions below would be reading IsEditingProfile's default rather than what Save did to it.
        vm.IsEditingProfile = true;
        return vm;
    }

    // ── The ordinary case is unchanged ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Update is tried first and, when it works, nothing is created. That order matters: creating first
    /// would hit the create handlers' name-based duplicate check on every ordinary edit and fail.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnExistingProfileIsUpdatedAndNotRecreated(bool isProvider)
    {
        var vm = Create(isProvider, out var provider, out var customer, updateSucceeds: true);

        await vm.SaveProfileCommand.ExecuteAsync(null);

        if (isProvider)
            provider.Verify(p => p.CreateProfileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        else
            customer.Verify(c => c.CreateProfileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.False(vm.IsEditingProfile);
        Assert.Equal(string.Empty, vm.ProfileErrorMessage);
    }

    // ── The recovery ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AProviderWithNoProfileGetsOneCreated()
    {
        var vm = Create(true, out var provider, out var customer, updateSucceeds: false);

        await vm.SaveProfileCommand.ExecuteAsync(null);

        provider.Verify(p => p.CreateProfileAsync(Email, "Ada", "Lovelace", "+15550100", It.IsAny<CancellationToken>()), Times.Once);
        // Never the other role's record: creating the wrong one leaves the account as broken as before.
        customer.Verify(c => c.CreateProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.False(vm.IsEditingProfile);
        Assert.Equal(string.Empty, vm.ProfileErrorMessage);
    }

    [Fact]
    public async Task ACustomerWithNoProfileGetsOneCreated()
    {
        var vm = Create(false, out var provider, out var customer, updateSucceeds: false);

        await vm.SaveProfileCommand.ExecuteAsync(null);

        customer.Verify(c => c.CreateProfileAsync(Email, "Ada", "Lovelace", "+15550100", It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.CreateProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.Equal(string.Empty, vm.ProfileErrorMessage);
    }

    /// <summary>
    /// An empty phone is sent as <c>null</c> on the create path, matching registration — a blank would
    /// persist as "this user has a phone number, and it is nothing".
    /// </summary>
    [Fact]
    public async Task AnEmptyPhoneIsCreatedAsNull()
    {
        var vm = Create(true, out var provider, out _, updateSucceeds: false);
        vm.PhoneNumber = "   ";

        await vm.SaveProfileCommand.ExecuteAsync(null);

        provider.Verify(p => p.CreateProfileAsync(Email, "Ada", "Lovelace", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Both attempts failing is a real failure and must still be reported rather than silently swallowed.
    [Fact]
    public async Task WhenBothUpdateAndCreateFail_ItReportsTheFailure()
    {
        var vm = Create(true, out _, out _, updateSucceeds: false, createSucceeds: false);

        await vm.SaveProfileCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(vm.ProfileErrorMessage));
        // Kept open, so the details the user typed are not lost behind a collapsed section.
        Assert.True(vm.IsEditingProfile);
    }

    [Fact]
    public async Task AnUnreachableServerIsReportedAsAConnectionProblem()
    {
        var provider = new Mock<IProviderApiService>();
        provider.Setup(p => p.UpdateProfileAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("no network"));

        var session = new Mock<IUserSessionService>();
        session.Setup(s => s.IsProvider).Returns(true);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);

        var vm = new AccountViewModel(
            provider.Object, Mock.Of<ICustomerApiService>(), Mock.Of<IAuthService>(), session.Object)
        {
            Email = Email,
            FirstName = "Ada",
            LastName = "Lovelace"
        };

        await vm.SaveProfileCommand.ExecuteAsync(null);

        Assert.Contains("connection", vm.ProfileErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsSavingProfile);
    }

    // A name is required either way — it is what the create handlers' duplicate check discriminates on, and
    // what every other screen shows for this account.
    [Theory]
    [InlineData("", "Lovelace")]
    [InlineData("Ada", "  ")]
    public void SavingIsBlockedWithoutAName(string firstName, string lastName)
    {
        var vm = Create(true, out _, out _, updateSucceeds: true);
        vm.FirstName = firstName;
        vm.LastName = lastName;

        Assert.False(vm.SaveProfileCommand.CanExecute(null));
    }
}
