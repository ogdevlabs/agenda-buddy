namespace AgendaBuddy.Library.Services;

public sealed record CustomerSetupSession(string CustomerId, string SessionId, string Url, bool CompletedLocally);
public sealed record SavedPaymentMethod(string PaymentMethodId, string Type, string? Brand, string? Last4);
public sealed record ProviderOnboardingSession(string AccountId, string Url, bool CompletedLocally);
public sealed record ProviderPayoutState(bool ChargesEnabled, bool PayoutsEnabled, string? DisabledReason);
public sealed record PaymentAuthorizationRequest(
    long AmountMinor,
    string Currency,
    string CustomerId,
    string PaymentMethodId,
    string ConnectedAccountId,
    long ApplicationFeeMinor,
    string Description,
    string IdempotencyKey);
public sealed record PaymentAuthorizationResult(string PaymentIntentId, string Status, DateTime? CaptureBefore = null);

public interface IPaymentGateway
{
    Task<CustomerSetupSession> CreateCustomerSetupSessionAsync(
        string email, string? customerId, string successUrl, string cancelUrl);
    Task<SavedPaymentMethod> CompleteCustomerSetupAsync(string sessionId, string customerId);
    Task<ProviderOnboardingSession> CreateProviderOnboardingSessionAsync(
        string email, string? accountId, string returnUrl, string refreshUrl);
    Task<ProviderPayoutState> GetProviderPayoutStateAsync(string accountId);
    Task<PaymentAuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request);
    Task<bool> CaptureAsync(string paymentIntentId, string idempotencyKey);
    Task<bool> CancelPaymentIntentAsync(string paymentIntentId);
    Task<bool> RefundPaymentIntentAsync(string paymentIntentId);
}
