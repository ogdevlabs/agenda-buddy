using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// The profile editor is the only screen that can repair a half-created account, and until it could
/// <b>create</b> a missing profile rather than only update an existing one, it could not.
/// </summary>
/// <remarks>
/// <para>
/// The hole: registration writes an Identity credential and then a domain profile. If the second call fails,
/// the user is signed in with a valid token, a correct role claim, and no profile. But
/// <c>UpdateProfileAsync</c> reads the profile before writing and gives up when the read 404s, and the route
/// behind it answers <c>NotFound</c>, because <c>FindOneAndUpdateAsync</c> never upserts (ADR-032 — deliberate,
/// and load-bearing elsewhere: it is what stops a failed login for an unknown address creating an account).
/// </para>
/// <para>
/// So every Save failed with "try again", and trying again could not help. The instruction was false, and the
/// account was unrecoverable from inside the app. Every account created before profile creation was wired at
/// all is in exactly that state.
/// </para>
/// <para>
/// These tests moved from <c>AccountViewModel</c> with the form itself: the Account screen became Profile, and
/// the editor became its own screen. The behaviour under test did not change, and that is the point of keeping
/// them.
/// </para>
/// </remarks>
public class EditProfileRecoveryTests
{
    private const string Email = "stranded@example.com";

    private static EditProfileViewModel Create(
        bool isProvider,
        out Mock<IProviderApiService> provider,
        out Mock<ICustomerApiService> customer,
        bool updateSucceeds,
        bool createSucceeds = true,
        bool consentSucceeds = true)
    {
        provider = new Mock<IProviderApiService>();
        provider.Setup(p => p.UpdateProfileAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(updateSucceeds);
        provider.Setup(p => p.CreateProfileAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(createSucceeds);
        provider.Setup(p => p.SetConsentAsync(
                    It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(consentSucceeds);

        customer = new Mock<ICustomerApiService>();
        customer.Setup(c => c.UpdateProfileAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(updateSucceeds);
        customer.Setup(c => c.CreateProfileAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(createSucceeds);
        customer.Setup(c => c.SetConsentAsync(
                    It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(consentSucceeds);

        var session = new Mock<IUserSessionService>();
        session.Setup(s => s.Email).Returns(Email);
        session.Setup(s => s.Role).Returns(isProvider ? "Provider" : "Customer");
        session.Setup(s => s.IsProvider).Returns(isProvider);
        session.Setup(s => s.IsCustomer).Returns(!isProvider);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);

        var vm = new EditProfileViewModel(provider.Object, customer.Object, session.Object);

        vm.Email = Email;
        vm.FirstName = "Ada";
        vm.LastName = "Lovelace";
        vm.PhoneNumber = "+15550100";
        // Both boxes ticked, because that is the only state Save can be reached from.
        vm.AcceptedTerms = true;
        vm.AcceptedPrivacy = true;
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
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        await vm.SaveCommand.ExecuteAsync(null);

        if (isProvider)
            provider.Verify(p => p.CreateProfileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        else
            customer.Verify(c => c.CreateProfileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.True(saved);
        Assert.Equal(string.Empty, vm.ErrorMessage);
    }

    // ── The recovery ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AProviderWithNoProfileGetsOneCreated()
    {
        var vm = Create(true, out var provider, out var customer, updateSucceeds: false);

        await vm.SaveCommand.ExecuteAsync(null);

        provider.Verify(p => p.CreateProfileAsync(Email, "Ada", "Lovelace", "+15550100", It.IsAny<CancellationToken>()), Times.Once);
        // Never the other role's record: creating the wrong one leaves the account as broken as before.
        customer.Verify(c => c.CreateProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.Equal(string.Empty, vm.ErrorMessage);
    }

    [Fact]
    public async Task ACustomerWithNoProfileGetsOneCreated()
    {
        var vm = Create(false, out var provider, out var customer, updateSucceeds: false);

        await vm.SaveCommand.ExecuteAsync(null);

        customer.Verify(c => c.CreateProfileAsync(Email, "Ada", "Lovelace", "+15550100", It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.CreateProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.Equal(string.Empty, vm.ErrorMessage);
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

        await vm.SaveCommand.ExecuteAsync(null);

        provider.Verify(p => p.CreateProfileAsync(Email, "Ada", "Lovelace", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Both attempts failing is a real failure and must still be reported rather than silently swallowed.
    [Fact]
    public async Task WhenBothUpdateAndCreateFail_ItReportsTheFailure()
    {
        var vm = Create(true, out _, out _, updateSucceeds: false, createSucceeds: false);
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
        // It stays on the page, so the details the user typed are not lost to a navigation.
        Assert.False(saved);
    }

    /// <summary>
    /// Consent is a second, separate write, and a failure there must not be reported as a total failure —
    /// the name change did land.
    /// </summary>
    [Fact]
    public async Task WhenConsentCannotBeRecorded_ItSaysSoWithoutClaimingTheDetailsFailed()
    {
        var vm = Create(true, out _, out _, updateSucceeds: true, consentSucceeds: false);
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains("agreement", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("saved", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(saved);
    }

    /// <summary>Consent goes to the caller's own role's route, and carries exactly what was ticked.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConsentIsRecordedForTheCallersOwnRole(bool isProvider)
    {
        var vm = Create(isProvider, out var provider, out var customer, updateSucceeds: true);

        await vm.SaveCommand.ExecuteAsync(null);

        if (isProvider)
        {
            provider.Verify(p => p.SetConsentAsync(Email, true, true, It.IsAny<CancellationToken>()), Times.Once);
            customer.Verify(c => c.SetConsentAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            customer.Verify(c => c.SetConsentAsync(Email, true, true, It.IsAny<CancellationToken>()), Times.Once);
            provider.Verify(p => p.SetConsentAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }
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

        var vm = new EditProfileViewModel(
            provider.Object, Mock.Of<ICustomerApiService>(), session.Object)
        {
            Email = Email,
            FirstName = "Ada",
            LastName = "Lovelace",
            AcceptedTerms = true,
            AcceptedPrivacy = true
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains("connection", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsSaving);
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

        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    /// <summary>
    /// Save is gated on both boxes rather than validated after the fact: an unticked box is a decision not yet
    /// made, and a disabled button beside it says what is missing without a message.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void SavingIsBlockedUntilBothAgreementsAreAccepted(bool acceptedTerms, bool acceptedPrivacy)
    {
        var vm = Create(true, out _, out _, updateSucceeds: true);
        vm.AcceptedTerms = acceptedTerms;
        vm.AcceptedPrivacy = acceptedPrivacy;

        Assert.False(vm.SaveCommand.CanExecute(null));
    }
}
