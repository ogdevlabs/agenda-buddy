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
        var viewModel = new PaymentAccountViewModel(api.Object, session.Object) { IsOnboarding = true };
        var completed = false;
        viewModel.OnboardingCompleted += (_, _) => completed = true;

        await viewModel.BeginSetupCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsReady);
        Assert.True(completed);
        Assert.Equal("Visa ending in 4242", viewModel.DisplayLabel);
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
}