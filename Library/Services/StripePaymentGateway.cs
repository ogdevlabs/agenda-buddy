using Stripe;

namespace Library.Services;

public class StripePaymentGateway(string apiKey) : IPaymentGateway
{
    private readonly PaymentIntentService _intents = new();

    public async Task<string> CreatePaymentIntentAsync(decimal amount, string currency, string description)
    {
        StripeConfiguration.ApiKey = apiKey;
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
