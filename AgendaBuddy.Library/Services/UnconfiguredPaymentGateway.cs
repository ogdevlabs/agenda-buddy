namespace AgendaBuddy.Library.Services;

public sealed class UnconfiguredPaymentGateway : IPaymentGateway
{
    private static InvalidOperationException Missing() => new(
        $"Payments are unavailable because {PaymentGatewayFactory.ApiKeyConfigurationKey} is not configured.");

    public Task<CustomerSetupSession> CreateCustomerSetupSessionAsync(
        string email, string? customerId, string successUrl, string cancelUrl) => Task.FromException<CustomerSetupSession>(Missing());

    public Task<SavedPaymentMethod> CompleteCustomerSetupAsync(string sessionId, string customerId) =>
        Task.FromException<SavedPaymentMethod>(Missing());

    public Task<ProviderOnboardingSession> CreateProviderOnboardingSessionAsync(
        string email, string? accountId, string returnUrl, string refreshUrl) =>
        Task.FromException<ProviderOnboardingSession>(Missing());

    public Task<ProviderPayoutState> GetProviderPayoutStateAsync(string accountId) =>
        Task.FromResult(new ProviderPayoutState(false, false, "Stripe is not configured"));

    public Task<PaymentAuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request) =>
        Task.FromException<PaymentAuthorizationResult>(Missing());

    public Task<bool> CaptureAsync(string paymentIntentId, string idempotencyKey) => Task.FromException<bool>(Missing());
    public Task<bool> CancelPaymentIntentAsync(string paymentIntentId) => Task.FromException<bool>(Missing());
    public Task<bool> RefundPaymentIntentAsync(string paymentIntentId) => Task.FromException<bool>(Missing());
}
