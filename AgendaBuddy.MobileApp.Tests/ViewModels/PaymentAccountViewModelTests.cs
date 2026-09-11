using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class PaymentAccountViewModelTests
{
    [Fact]
    public async Task BeginSetup_LocalCustomerCompletion_ReloadsAndCompletesOnboarding()
    {
        var api = new Mock<IPaymentAccountApiService>();
        api.Setup(a => a.BeginCustomerSetupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentOnboardingLink("agendame://payment-method/complete", true));
        api.Setup(a => a.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentAccountStatus("Customer", true, "Payment method ready", "card", "Visa", "4242"));
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.IsProvider).Returns(false);
        var alerts = new Mock<IInAppAlertService>();
        var viewModel = new PaymentAccountViewModel(api.Object, session.Object, alerts.Object) { IsOnboarding = true };
        var completed = false;
        viewModel.OnboardingCompleted += (_, _) => completed = true;

        await viewModel.BeginSetupCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsReady);
        Assert.True(completed);
        Assert.Equal("Visa ending in 4242", viewModel.DisplayLabel);
        alerts.Verify(a => a.ShowAsync(It.Is<string>(message =>
            message.Contains("simulated", StringComparison.OrdinalIgnoreCase))), Times.Once);
    }

    [Fact]
    public async Task BeginSetup_ProviderHostedFlow_RequestsExternalUrl()
    {
        var api = new Mock<IPaymentAccountApiService>();
        api.Setup(a => a.BeginProviderOnboardingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentOnboardingLink("https://connect.stripe.test/onboard", false));
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.IsProvider).Returns(true);
        var viewModel = new PaymentAccountViewModel(api.Object, session.Object);
        Uri? opened = null;
        viewModel.OpenUrlRequested += (_, uri) => opened = uri;

        await viewModel.BeginSetupCommand.ExecuteAsync(null);

        Assert.Equal("https://connect.stripe.test/onboard", opened?.AbsoluteUri);
        api.Verify(a => a.BeginCustomerSetupAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BeginSetup_WhenStripeIsUnavailable_ExplainsThatItCanBeDoneLater()
    {
        var api = new Mock<IPaymentAccountApiService>();
        api.Setup(a => a.BeginCustomerSetupAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PaymentSetupUnavailableException());
        var session = new Mock<IUserSessionService>();
        var viewModel = new PaymentAccountViewModel(api.Object, session.Object) { IsOnboarding = true };
        var skipped = false;
        viewModel.OnboardingSkipped += (_, _) => skipped = true;

        await viewModel.BeginSetupCommand.ExecuteAsync(null);
        viewModel.SkipOnboardingCommand.Execute(null);

        Assert.Contains("not configured", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.ShowSkip);
        Assert.False(skipped);
    }

    [Fact]
    public async Task SkipOnboarding_LocalDevelopment_DoesNotMarkPaymentReadyAndSignalsNavigation()
    {
        var api = new Mock<IPaymentAccountApiService>();
        api.Setup(a => a.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentAccountStatus(
                "Customer", false, "Payment method required", null, null, null, CanSkipOnboarding: true));
        var viewModel = new PaymentAccountViewModel(
            api.Object,
            Mock.Of<IUserSessionService>())
        {
            IsOnboarding = true
        };
        var skipped = false;
        viewModel.OnboardingSkipped += (_, _) => skipped = true;

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.SkipOnboardingCommand.Execute(null);

        Assert.True(viewModel.ShowSkip);
        Assert.True(skipped);
        Assert.False(viewModel.IsReady);
    }
}
