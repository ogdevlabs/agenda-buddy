using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using AgendaBuddy.MobileApp.Resources.Strings;
using System.Globalization;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class EmailVerificationViewModelTests
{
    [Fact]
    public async Task ValidTokenShowsVerifiedState()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.ConfirmEmailAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var viewModel = new EmailVerificationViewModel(auth.Object) { Token = "valid-token" };
        var succeeded = false;
        viewModel.VerificationSucceeded += () => succeeded = true;

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsVerified);
        Assert.True(succeeded);
        Assert.Contains("verified", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidTokenStaysUnauthenticatedAndExplainsExpiry()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.ConfirmEmailAsync("expired-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var viewModel = new EmailVerificationViewModel(auth.Object) { Token = "expired-token" };

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsVerified);
        Assert.Contains("invalid or has expired", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResendUsesPendingEmailAndKeepsAnAntiEnumeratingMessage()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.RequestEmailVerificationAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var viewModel = new EmailVerificationViewModel(auth.Object) { Email = " user@example.com " };

        await viewModel.ResendCommand.ExecuteAsync(null);

        auth.Verify(a => a.RequestEmailVerificationAsync(
            "user@example.com", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("If this account", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifiedStateUsesTheActiveSpanishCulture()
    {
        var previousCulture = AppResources.Culture;
        try
        {
            AppResources.Culture = CultureInfo.GetCultureInfo("es-MX");
            var auth = new Mock<IAuthService>();
            auth.Setup(a => a.ConfirmEmailAsync("valid-token", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var viewModel = new EmailVerificationViewModel(auth.Object) { Token = "valid-token" };

            await viewModel.ConfirmCommand.ExecuteAsync(null);

            Assert.Equal("Correo confirmado. Inicia sesión para continuar.", viewModel.StatusMessage);
        }
        finally
        {
            AppResources.Culture = previousCulture;
        }
    }

    [Fact]
    public async Task SixDigitCodeConfirmsForThePendingEmailAndSignalsNavigation()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.ConfirmEmailCodeAsync(
                "user@example.com", "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var viewModel = new EmailVerificationViewModel(auth.Object)
        {
            Email = "user@example.com",
            Code = "123456"
        };
        var succeeded = false;
        viewModel.VerificationSucceeded += () => succeeded = true;

        await viewModel.ConfirmCodeCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsVerified);
        Assert.True(succeeded);
        auth.Verify(a => a.ConfirmEmailCodeAsync(
            "user@example.com", "123456", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("12345x")]
    public void ConfirmationCodeMustBeExactlySixDigits(string code)
    {
        var viewModel = new EmailVerificationViewModel(Mock.Of<IAuthService>())
        {
            Email = "user@example.com",
            Code = code
        };

        Assert.False(viewModel.ConfirmCodeCommand.CanExecute(null));
    }
}