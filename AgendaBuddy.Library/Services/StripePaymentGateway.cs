using Stripe;

namespace AgendaBuddy.Library.Services;

/// <summary>
/// The real gateway. Selected only when <see cref="PaymentGatewayFactory.ApiKeyConfigurationKey"/> is
/// configured — see <see cref="RecordingPaymentGateway"/> for what runs otherwise.
/// </summary>
/// <remarks>
/// <b>The API key is assigned once, at construction.</b> It used to be assigned inside
/// <see cref="CreatePaymentIntentAsync"/>, and <c>StripeConfiguration.ApiKey</c> is a <b>process-global
/// static</b>: writing a live payment credential to a global from request handling makes the key's lifetime
/// the process's rather than the call's, and makes the assignment a data race the moment two requests
/// overlap. Construction happens once per registration, which is the narrowest this can be without the
/// Stripe SDK growing a per-client key.
/// </remarks>
public class StripePaymentGateway : IPaymentGateway
{
    private readonly PaymentIntentService _intents = new();

    public StripePaymentGateway(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        StripeConfiguration.ApiKey = apiKey;
    }

    public async Task<string> CreatePaymentIntentAsync(decimal amount, string currency, string description)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = (long)(amount * 100),
            Currency = currency,
            Description = description,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
        };
        var intent = await _intents.CreateAsync(options);
        return intent.Id;
    }

    public async Task<bool> ConfirmPaymentIntentAsync(string paymentIntentId)
    {
        var intent = await _intents.GetAsync(paymentIntentId);
        return intent.Status is "succeeded" or "processing";
    }

    public async Task<bool> RefundPaymentIntentAsync(string paymentIntentId)
    {
        var refundSvc = new RefundService();
        var refund = await refundSvc.CreateAsync(new RefundCreateOptions { PaymentIntent = paymentIntentId });
        return refund.Status == "succeeded";
    }
}
