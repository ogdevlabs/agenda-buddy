using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// Registration has to produce <b>two</b> records: an Identity credential and the matching domain profile.
/// </summary>
/// <remarks>
/// <para>
/// This suite exists because the second one was missing and nothing noticed. Registering created a
/// credential only, so a provider could not pass the profession gate and a customer could not subscribe to
/// anyone — every domain read answered 404 against a profile that did not exist, while the JWT was perfectly
/// valid and the role claim correct. That is the shape that makes it hard to see: it does not look like an
/// auth problem, and it does not look like a bug in the screen that fails.
/// </para>
/// <para>
/// The fix landed the same day it was filed and shipped with <b>no regression coverage at all</b>, which is
/// what these tests are for. Identity is deliberately decoupled from the domain services and there is no
/// message broker, so the client is the only thing that can make both writes happen — which means the client
/// is where the guarantee has to be asserted.
/// </para>
/// </remarks>
public class RegisterViewModelTests
{
    private const string Email = "new.user@example.com";
    private const string Password = "correct-horse-battery-staple";

    private static RegisterViewModel Create(
        out Mock<IAuthService> auth,
        out Mock<IProviderApiService> provider,
        out Mock<ICustomerApiService> customer,
        bool registerSucceeds = true,
        bool profileSucceeds = true)
    {
        auth = new Mock<IAuthService>();
        auth.Setup(a => a.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(registerSucceeds);

        provider = new Mock<IProviderApiService>();
        provider.Setup(p => p.CreateProfileAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(profileSucceeds);

        customer = new Mock<ICustomerApiService>();
        customer.Setup(c => c.CreateProfileAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(profileSucceeds);

        return new RegisterViewModel(auth.Object, provider.Object, customer.Object);
    }

    private static void Fill(RegisterViewModel vm, bool isProvider, string phone = "")
    {
        vm.FirstName = "Ada";
        vm.LastName = "Lovelace";
        vm.Email = Email;
        vm.Password = Password;
        vm.ConfirmPassword = Password;
        vm.PhoneNumber = phone;
        vm.IsProvider = isProvider;
    }

    // ── The defect this suite exists for ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisteringAsProvider_CreatesTheProviderProfile()
    {
        var vm = Create(out var auth, out var provider, out var customer);
        Fill(vm, isProvider: true);

        await vm.RegisterCommand.ExecuteAsync(null);

        auth.Verify(a => a.RegisterAsync(Email, Password, "Provider", It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.CreateProfileAsync(Email, "Ada", "Lovelace", null, It.IsAny<CancellationToken>()), Times.Once);
        // Never the other role's profile: the role picker chooses which record exists, and creating the
        // wrong one leaves the account just as broken as creating none.
        customer.Verify(c => c.CreateProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task RegisteringAsCustomer_CreatesTheCustomerProfile()
    {
        var vm = Create(out var auth, out var provider, out var customer);
        Fill(vm, isProvider: false);

        await vm.RegisterCommand.ExecuteAsync(null);

        auth.Verify(a => a.RegisterAsync(Email, Password, "Customer", It.IsAny<CancellationToken>()), Times.Once);
        customer.Verify(c => c.CreateProfileAsync(Email, "Ada", "Lovelace", null, It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.CreateProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(vm.HasError);
    }

    /// <summary>
    /// The role string is what Identity stores as the role claim, and it is compared case-sensitively
    /// downstream (<c>IUserSessionService.IsProvider</c> uses <c>OrdinalIgnoreCase</c>, but the domain
    /// services' role checks do not all agree). Pinned exactly rather than "contains provider".
    /// </summary>
    [Theory]
    [InlineData(true, "Provider")]
    [InlineData(false, "Customer")]
    public async Task TheRoleSentToIdentityMatchesThePicker(bool isProvider, string expectedRole)
    {
        var vm = Create(out var auth, out _, out _);
        Fill(vm, isProvider);

        await vm.RegisterCommand.ExecuteAsync(null);

        auth.Verify(a => a.RegisterAsync(Email, Password, expectedRole, It.IsAny<CancellationToken>()), Times.Once);
    }

    // A failed credential creation must not go on to create a profile for an account that does not exist.
    [Fact]
    public async Task WhenTheCredentialCannotBeCreated_NoProfileIsAttempted()
    {
        var vm = Create(out _, out var provider, out var customer, registerSucceeds: false);
        Fill(vm, isProvider: true);

        await vm.RegisterCommand.ExecuteAsync(null);

        provider.Verify(p => p.CreateProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        customer.Verify(c => c.CreateProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(vm.HasError);
    }

    /// <summary>
    /// A profile that could not be created is reported, and the message has to name the way out — this is
    /// the half-created account, and it is recoverable only because <c>EditProfileViewModel</c> now creates a
    /// missing profile rather than only updating one.
    /// </summary>
    [Fact]
    public async Task WhenTheProfileCannotBeCreated_ItSaysSoAndPointsAtTheRecovery()
    {
        var vm = Create(out _, out _, out _, profileSucceeds: false);
        Fill(vm, isProvider: true);

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("Account", vm.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// The account exists and the caller is signed in, so a failed profile write must still let them in
    /// rather than stranding them on the register screen with tokens already stored.
    /// </summary>
    [Fact]
    public async Task AFailedProfileStillCompletesRegistration()
    {
        var vm = Create(out _, out _, out _, profileSucceeds: false);
        Fill(vm, isProvider: false);

        var completed = false;
        vm.RegistrationSucceeded += (_, _) => completed = true;

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.True(completed);
    }

    // ── Input handling ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Phone is optional, and an omitted one must be sent as <c>null</c> rather than <c>""</c> — the entity
    /// carries an <c>[EmailAddress]</c>-style annotation set where an empty string is a value and null is
    /// absence, so a blank would persist as "the user has a phone number, and it is nothing".
    /// </summary>
    [Fact]
    public async Task AnOmittedPhoneNumberIsSentAsNull()
    {
        var vm = Create(out _, out var provider, out _);
        Fill(vm, isProvider: true, phone: "   ");

        await vm.RegisterCommand.ExecuteAsync(null);

        provider.Verify(p => p.CreateProfileAsync(Email, "Ada", "Lovelace", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APhoneNumberIsTrimmedAndSent()
    {
        var vm = Create(out _, out var provider, out _);
        Fill(vm, isProvider: true, phone: "  +15550100  ");

        await vm.RegisterCommand.ExecuteAsync(null);

        provider.Verify(p => p.CreateProfileAsync(Email, "Ada", "Lovelace", "+15550100", It.IsAny<CancellationToken>()), Times.Once);
    }

    // Names are trimmed before they become the record everyone else reads off a booking.
    [Fact]
    public async Task NamesAreTrimmed()
    {
        var vm = Create(out _, out var provider, out _);
        Fill(vm, isProvider: true);
        vm.FirstName = "  Ada  ";
        vm.LastName = "  Lovelace  ";

        await vm.RegisterCommand.ExecuteAsync(null);

        provider.Verify(p => p.CreateProfileAsync(Email, "Ada", "Lovelace", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Guards that run before anything is created ─────────────────────────────────────────────────

    [Fact]
    public async Task MismatchedPasswords_CreateNothing()
    {
        var vm = Create(out var auth, out var provider, out _);
        Fill(vm, isProvider: true);
        vm.ConfirmPassword = "something-else";

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Equal("Passwords do not match.", vm.ErrorMessage);
        auth.Verify(a => a.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        provider.Verify(p => p.CreateProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AShortPassword_CreatesNothing()
    {
        var vm = Create(out var auth, out _, out _);
        Fill(vm, isProvider: true);
        vm.Password = "short";
        vm.ConfirmPassword = "short";

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Contains("8 characters", vm.ErrorMessage, StringComparison.Ordinal);
        auth.Verify(a => a.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A name is required, because it is what the other party sees on a booking and an account without one
    /// has nothing human-readable to identify it by anywhere in the app. It is also what stops the create
    /// handlers' name-based duplicate check from matching two nameless profiles against each other.
    /// </summary>
    [Theory]
    [InlineData("", "Lovelace")]
    [InlineData("Ada", "")]
    [InlineData("   ", "Lovelace")]
    public void RegistrationIsBlockedWithoutAName(string firstName, string lastName)
    {
        var vm = Create(out _, out _, out _);
        Fill(vm, isProvider: true);
        vm.FirstName = firstName;
        vm.LastName = lastName;

        Assert.False(vm.RegisterCommand.CanExecute(null));
    }

    [Fact]
    public void RegistrationIsAllowedWithoutAPhoneNumber()
    {
        var vm = Create(out _, out _, out _);
        Fill(vm, isProvider: true, phone: "");

        Assert.True(vm.RegisterCommand.CanExecute(null));
    }

    // An unreachable server is reported as such, not as "this email may already be in use".
    [Fact]
    public async Task AnUnreachableServerIsReportedAsAConnectionProblem()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("no network"));

        var vm = new RegisterViewModel(
            auth.Object, Mock.Of<IProviderApiService>(), Mock.Of<ICustomerApiService>());
        Fill(vm, isProvider: true);

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Contains("connection", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsLoading);
    }

    // ── Timezone at signup ─────────────────────────────────────────────────────────────────────────
    // A provider's availability window is generated in THEIR zone. It used to be recorded only when they
    // happened to open Account, so a provider who never did kept UTC hours and every slot offered to their
    // customers was wrong by their offset.

    [Fact]
    public async Task RegisteringAsProvider_RecordsTheDeviceTimeZone()
    {
        var vm = Create(out _, out var provider, out _);
        Fill(vm, isProvider: true);

        await vm.RegisterCommand.ExecuteAsync(null);

        provider.Verify(p => p.SyncTimeZoneAsync(Email, It.IsAny<CancellationToken>()), Times.Once);
    }

    // A customer has no availability window, so there is nothing for a zone to mean on their profile.
    [Fact]
    public async Task RegisteringAsCustomer_DoesNotSyncATimeZone()
    {
        var vm = Create(out _, out var provider, out _);
        Fill(vm, isProvider: false);

        await vm.RegisterCommand.ExecuteAsync(null);

        provider.Verify(p => p.SyncTimeZoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // No profile means nothing to attach a zone to.
    [Fact]
    public async Task WhenTheProfileFailed_NoTimeZoneIsSynced()
    {
        var vm = Create(out _, out var provider, out _, profileSucceeds: false);
        Fill(vm, isProvider: true);

        await vm.RegisterCommand.ExecuteAsync(null);

        provider.Verify(p => p.SyncTimeZoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A failed zone sync must not fail registration: the profile is the thing that had to be created, and
    /// the zone is re-synced on the next Account load.
    /// </summary>
    [Fact]
    public async Task AFailedTimeZoneSyncDoesNotFailRegistration()
    {
        var vm = Create(out _, out var provider, out _);
        provider.Setup(p => p.SyncTimeZoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("no network"));
        Fill(vm, isProvider: true);

        var completed = false;
        vm.RegistrationSucceeded += (_, _) => completed = true;

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.True(completed);
        Assert.False(vm.HasError);
    }
}
