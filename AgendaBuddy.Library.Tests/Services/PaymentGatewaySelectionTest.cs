using System.Collections.Generic;
using System.Threading.Tasks;
using AgendaBuddy.Library.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

/// <summary>
/// Payments are non-charging unless a key is configured, and the absence is
/// announced.
/// </summary>
/// <remarks>
/// There is no Stripe account, no key and no deployment (ADR-035 defers cloud until every pending feature
/// ships). The two alternatives to a non-charging default both fail: a gateway that throws leaves
/// <c>PaymentService</c> unreachable — the exact condition this default exists to prevent — and a gateway that charges by
/// default is unthinkable without an account.
/// </remarks>
public class PaymentGatewaySelectionTest
{
    private static IConfiguration ConfigurationWith(string? apiKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [PaymentGatewayFactory.ApiKeyConfigurationKey] = apiKey
            })
            .Build();

    [Fact]
    public void T206_WithNoConfiguredKey_TheGatewayDoesNotCharge()
    {
        Assert.Equal(PaymentGatewayMode.Recording, PaymentGatewayFactory.ModeFor(ConfigurationWith(null)));
        Assert.IsType<RecordingPaymentGateway>(PaymentGatewayFactory.Create(ConfigurationWith(null)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void T206_AnEmptyOrWhitespaceKeyIsNotAKey(string apiKey)
    {
        // A deployment that sets the variable to an empty string has not configured Stripe, and must not be
        // treated as having done so — StripePaymentGateway would then throw at construction and take the whole
        // service down on startup.
        Assert.Equal(PaymentGatewayMode.Recording, PaymentGatewayFactory.ModeFor(ConfigurationWith(apiKey)));
    }

    [Fact]
    public void WithAConfiguredKey_StripeIsSelected()
    {
        // Not constructed here: StripePaymentGateway assigns the process-global StripeConfiguration.ApiKey at
        // construction, and a test has no business mutating that for the rest of the run. Selection is the part
        // this test owns.
        Assert.Equal(
            PaymentGatewayMode.Stripe,
            PaymentGatewayFactory.ModeFor(ConfigurationWith("sk_test_not_a_real_key")));
    }

    [Fact]
    public void T206_ADeploymentWithNoKey_IsWarnedAbout_NamingTheKey()
    {
        // PRD risk R4: the residual risk of a non-charging default is that it becomes permanent — payments
        // recorded that never happened, while every artifact says the feature is delivered. Same mitigation
        // as ADR-033: warn loudly rather than fail to start.
        var warning = PaymentGatewayFactory.RecordingModeWarning(ConfigurationWith(null), isLocalRun: false);

        Assert.NotNull(warning);
        Assert.Contains(PaymentGatewayFactory.ApiKeyConfigurationKey, warning, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ALocalRunIsNotWarnedAbout()
    {
        Assert.Null(PaymentGatewayFactory.RecordingModeWarning(ConfigurationWith(null), isLocalRun: true));
    }

    [Fact]
    public void ADeploymentWithAKeyIsNotWarnedAbout()
    {
        Assert.Null(PaymentGatewayFactory.RecordingModeWarning(
            ConfigurationWith("sk_test_not_a_real_key"), isLocalRun: false));
    }

    [Fact]
    public async Task TheRecordingGatewayMarksItsIntents_SoAStoredPaymentSaysItWasNeverCharged()
    {
        // The signal lives in the stored data, not only in a log: `local_` cannot be produced by Stripe (its
        // ids begin `pi_`), so a payment recorded under this gateway is permanently identifiable as one that
        // moved no money. A UI that says "Paid" on it is lying to a provider about their income.
        var gateway = new RecordingPaymentGateway();

        var intentId = await gateway.CreatePaymentIntentAsync(50m, "gbp", "Appointment a7f3");

        Assert.StartsWith(RecordingPaymentGateway.LocalIntentPrefix, intentId);
        Assert.True(await gateway.ConfirmPaymentIntentAsync(intentId));
        Assert.True(await gateway.RefundPaymentIntentAsync(intentId));
    }

    [Fact]
    public async Task EachRecordedIntentIsDistinct()
    {
        var gateway = new RecordingPaymentGateway();

        var first = await gateway.CreatePaymentIntentAsync(50m, "gbp", "one");
        var second = await gateway.CreatePaymentIntentAsync(50m, "gbp", "two");

        Assert.NotEqual(first, second);
    }
}
