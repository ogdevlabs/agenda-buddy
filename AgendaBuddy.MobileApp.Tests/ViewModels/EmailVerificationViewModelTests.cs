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

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsVerified);
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
}