using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class RegisterViewModelTests
{
    private const string Email = "new.user@example.com";
    private const string Password = "correct-horse-battery-staple";

    private static RegisterViewModel Create(
        out Mock<IAuthService> auth,
        out Mock<IProviderApiService> provider,
        out Mock<ICustomerApiService> customer,
        bool registerSucceeds = true)
    {
        auth = new Mock<IAuthService>();
        auth.Setup(a => a.RegisterAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(registerSucceeds);
        provider = new Mock<IProviderApiService>();
        customer = new Mock<ICustomerApiService>();
        return new RegisterViewModel(auth.Object, provider.Object, customer.Object);
    }

    private static void Fill(RegisterViewModel viewModel, bool isProvider)
    {
        viewModel.FirstName = "Ada";
        viewModel.LastName = "Lovelace";
        viewModel.Email = Email;
        viewModel.Password = Password;
        viewModel.ConfirmPassword = Password;
        viewModel.IsProvider = isProvider;
    }

    [Theory]
    [InlineData(true, "Provider")]
    [InlineData(false, "Customer")]
    public async Task SuccessfulRegistrationRequestsVerificationForTheSelectedRole(
        bool isProvider,
        string expectedRole)
    {
        var viewModel = Create(out var auth, out _, out _);
        Fill(viewModel, isProvider);
        string? pendingEmail = null;
        viewModel.VerificationPending += email => pendingEmail = email;

        await viewModel.RegisterCommand.ExecuteAsync(null);

        auth.Verify(a => a.RegisterAsync(
            Email, Password, expectedRole, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(Email, pendingEmail);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task RegistrationDoesNotCreateAProfileBeforeEmailVerificationAndLogin()
    {
        var viewModel = Create(out _, out var provider, out var customer);
        Fill(viewModel, isProvider: true);

        await viewModel.RegisterCommand.ExecuteAsync(null);

        provider.Verify(p => p.CreateProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        customer.Verify(c => c.CreateProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        provider.Verify(p => p.SyncTimeZoneAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuccessfulRegistrationStoresTheProfileDraftForVerifiedLogin()
    {
        var viewModel = Create(out var auth, out var provider, out var customer);
        var store = new Mock<IPendingRegistrationStore>();
        viewModel = new RegisterViewModel(auth.Object, provider.Object, customer.Object, store.Object);
        Fill(viewModel, isProvider: true);
        viewModel.PhoneNumber = "  +15550100  ";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        store.Verify(s => s.SaveAsync(new PendingRegistration(
            Email, "Ada", "Lovelace", "+15550100", "Provider")), Times.Once);
    }

    [Fact]
    public async Task FailedRegistrationStaysOnTheForm()
    {
        var viewModel = Create(out _, out _, out _, registerSucceeds: false);
        Fill(viewModel, isProvider: true);
        var pendingRaised = false;
        viewModel.VerificationPending += _ => pendingRaised = true;

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.False(pendingRaised);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task MismatchedPasswordsDoNotCallIdentity()
    {
        var viewModel = Create(out var auth, out _, out _);
        Fill(viewModel, isProvider: true);
        viewModel.ConfirmPassword = "something-else";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal("Passwords do not match.", viewModel.ErrorMessage);
        auth.Verify(a => a.RegisterAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ShortPasswordDoesNotCallIdentity()
    {
        var viewModel = Create(out var auth, out _, out _);
        Fill(viewModel, isProvider: true);
        viewModel.Password = "short";
        viewModel.ConfirmPassword = "short";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Contains("8 characters", viewModel.ErrorMessage, StringComparison.Ordinal);
        auth.Verify(a => a.RegisterAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("", "Lovelace")]
    [InlineData("Ada", "")]
    [InlineData("   ", "Lovelace")]
    public void RegistrationIsBlockedWithoutAName(string firstName, string lastName)
    {
        var viewModel = Create(out _, out _, out _);
        Fill(viewModel, isProvider: true);
        viewModel.FirstName = firstName;
        viewModel.LastName = lastName;

        Assert.False(viewModel.RegisterCommand.CanExecute(null));
    }

    [Fact]
    public async Task UnreachableServerIsReportedAsAConnectionProblem()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.RegisterAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("no network"));
        var viewModel = new RegisterViewModel(
            auth.Object, Mock.Of<IProviderApiService>(), Mock.Of<ICustomerApiService>());
        Fill(viewModel, isProvider: true);

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Contains("connection", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.IsLoading);
    }
}
