using AgendaBuddy.MobileApp.Infrastructure;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class PaymentOnboardingCallbackTests
{
    [Fact]
    public void CustomerCompletion_ExtractsDecodedSessionId()
    {
        var callback = PaymentOnboardingCallback.Parse(
            new Uri("agendame://payment-method/complete?session_id=cs_test_123%2Fabc"));

        Assert.Equal(PaymentOnboardingCallbackKind.CustomerCompleted, callback.Kind);
        Assert.Equal("cs_test_123/abc", callback.SessionId);
    }

    [Theory]
    [InlineData("agendame://payment-method/cancelled", PaymentOnboardingCallbackKind.CustomerCancelled)]
    [InlineData("agendame://provider-payout/complete", PaymentOnboardingCallbackKind.ProviderCompleted)]
    [InlineData("agendame://provider-payout/refresh", PaymentOnboardingCallbackKind.ProviderRefresh)]
    [InlineData("https://example.com/payment-method/complete", PaymentOnboardingCallbackKind.Unknown)]
    public void Parse_RecognizesOnlyAgendaMePaymentRoutes(
        string value, PaymentOnboardingCallbackKind expected)
    {
        Assert.Equal(expected, PaymentOnboardingCallback.Parse(new Uri(value)).Kind);
    }
}