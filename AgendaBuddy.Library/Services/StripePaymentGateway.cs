using Stripe;
using Stripe.Checkout;
using StripeCustomerService = Stripe.CustomerService;
using StripeSessionService = Stripe.Checkout.SessionService;

namespace AgendaBuddy.Library.Services;

/// <summary>
/// The real gateway. Selected only when <see cref="PaymentGatewayFactory.ApiKeyConfigurationKey"/> is
/// configured — see <see cref="RecordingPaymentGateway"/> for what runs otherwise.
/// </summary>
public class StripePaymentGateway : IPaymentGateway
{
    private readonly PaymentIntentService _intents = new();

    public bool AllowsSkippingOnboarding => false;

    public async Task<CustomerSetupSession> CreateCustomerSetupSessionAsync(
        string email, string? customerId, string successUrl, string cancelUrl)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            var customer = await new StripeCustomerService().CreateAsync(new CustomerCreateOptions
            {
                Email = email,
                Metadata = new Dictionary<string, string> { ["agenda_me_email"] = email }
            });
            customerId = customer.Id;
        }

        var session = await new StripeSessionService().CreateAsync(new SessionCreateOptions
        {
            Mode = "setup",
            Customer = customerId,
            Currency = "usd",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            SetupIntentData = new SessionSetupIntentDataOptions
            {
                Metadata = new Dictionary<string, string> { ["agenda_me_email"] = email }
            }
        });

        return new CustomerSetupSession(customerId, session.Id, session.Url, false);
    }

    public async Task<SavedPaymentMethod> CompleteCustomerSetupAsync(string sessionId, string customerId)
    {
        var session = await new StripeSessionService().GetAsync(sessionId);
        if (!string.Equals(session.CustomerId, customerId, StringComparison.Ordinal)
            || !string.Equals(session.Status, "complete", StringComparison.Ordinal))
            throw new InvalidOperationException("Stripe has not completed this payment-method setup.");

        var setupIntent = await new SetupIntentService().GetAsync(session.SetupIntentId);
        if (!string.Equals(setupIntent.Status, "succeeded", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(setupIntent.PaymentMethodId))
            throw new InvalidOperationException("Stripe did not save a reusable payment method.");

        var method = await new PaymentMethodService().GetAsync(setupIntent.PaymentMethodId);
        return new SavedPaymentMethod(method.Id, method.Type, method.Card?.Brand, method.Card?.Last4);
    }

    public async Task<ProviderOnboardingSession> CreateProviderOnboardingSessionAsync(
        string email, string? accountId, string returnUrl, string refreshUrl)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            var account = await new AccountService().CreateAsync(new AccountCreateOptions
            {
                Type = "express",
                Email = email,
                Capabilities = new AccountCapabilitiesOptions
                {
                    CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                    Transfers = new AccountCapabilitiesTransfersOptions { Requested = true }
                },
                BusinessProfile = new AccountBusinessProfileOptions
                {
                    ProductDescription = "Independent services booked through AgendaMe"
                },
                Metadata = new Dictionary<string, string> { ["agenda_me_email"] = email }
            });
            accountId = account.Id;
        }

        var link = await new AccountLinkService().CreateAsync(new AccountLinkCreateOptions
        {
            Account = accountId,
            Type = "account_onboarding",
            ReturnUrl = returnUrl,
            RefreshUrl = refreshUrl
        });
        return new ProviderOnboardingSession(accountId, link.Url, false);
    }

    public async Task<ProviderPayoutState> GetProviderPayoutStateAsync(string accountId)
    {
        var account = await new AccountService().GetAsync(accountId);
        return new ProviderPayoutState(
            account.ChargesEnabled,
            account.PayoutsEnabled,
            account.Requirements?.DisabledReason);
    }

    public async Task<PaymentAuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request)
    {
        try
        {
            var intent = await _intents.CreateAsync(new PaymentIntentCreateOptions
            {
                Amount = request.AmountMinor,
                Currency = request.Currency,
                Customer = request.CustomerId,
                PaymentMethod = request.PaymentMethodId,
                Confirm = true,
                OffSession = true,
                CaptureMethod = "manual",
                ApplicationFeeAmount = request.ApplicationFeeMinor,
                TransferData = new PaymentIntentTransferDataOptions
                {
                    Destination = request.ConnectedAccountId
                },
                Description = request.Description,
                Metadata = new Dictionary<string, string>
                {
                    ["appointment_identifier"] = request.IdempotencyKey
                }
            }, new RequestOptions { IdempotencyKey = $"authorize:{request.IdempotencyKey}" });

            return new PaymentAuthorizationResult(
                intent.Id,
                intent.Status,
                intent.LatestCharge?.PaymentMethodDetails?.Card?.CaptureBefore);
        }
        catch (StripeException exception) when (exception.StripeError?.PaymentIntent is { } intent)
        {
            return new PaymentAuthorizationResult(
                intent.Id,
                intent.Status,
                intent.LatestCharge?.PaymentMethodDetails?.Card?.CaptureBefore);
        }
    }

    public async Task<bool> CaptureAsync(string paymentIntentId, string idempotencyKey)
    {
        var intent = await _intents.CaptureAsync(
            paymentIntentId,
            options: null,
            new RequestOptions { IdempotencyKey = $"capture:{idempotencyKey}" });
        return intent.Status is "succeeded" or "processing";
    }

    public StripePaymentGateway(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        StripeConfiguration.ApiKey = apiKey;
    }

    public async Task<bool> CancelPaymentIntentAsync(string paymentIntentId)
    {
        var intent = await _intents.CancelAsync(paymentIntentId);
        return intent.Status == "canceled";
    }

    public async Task<bool> RefundPaymentIntentAsync(string paymentIntentId)
    {
        var refundSvc = new RefundService();
        var refund = await refundSvc.CreateAsync(new RefundCreateOptions
        {
            PaymentIntent = paymentIntentId,
            ReverseTransfer = true,
            RefundApplicationFee = true
        });
        return refund.Status == "succeeded";
    }
}
