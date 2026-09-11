using System.Security.Cryptography;

namespace AgendaBuddy.Library.Services;

/// <summary>
/// An <see cref="IPaymentGateway"/> that records a payment locally and contacts nothing.
/// </summary>
/// <remarks>
/// <para>
/// The default payment gateway. It exists so that <c>PaymentService</c> can be exercised by a developer
/// and by a test without a Stripe account, a live secret key, or any possibility of moving money.
/// </para>
/// <para>
/// <b>It reports success.</b> That is deliberate and it is the risk: a caller cannot tell a recorded
/// payment from a settled one by its <c>status</c> alone. The signal is the intent id, which is prefixed
/// <see cref="LocalIntentPrefix"/> — a value Stripe cannot produce. `api-contracts.md` §2 states this in the
/// contract rather than leaving a client to infer it, and
/// <see cref="PaymentGatewayFactory.RecordingModeWarning"/> says it at startup outside a local run.
/// </para>
/// <para>
/// The alternative — failing every charge — would leave the capability unreachable.
/// </para>
/// </remarks>
public sealed class RecordingPaymentGateway : IPaymentGateway
{
    public bool AllowsSkippingOnboarding => true;

    /// <summary>
    /// Marks an intent id as locally generated. Stripe's ids begin <c>pi_</c>, so this cannot collide with a
    /// real one, and its presence in stored data is a permanent record that the payment was never charged.
    /// </summary>
    public const string LocalIntentPrefix = "local_";

    public Task<CustomerSetupSession> CreateCustomerSetupSessionAsync(
        string email, string? customerId, string successUrl, string cancelUrl)
    {
        var id = customerId ?? $"cus_local_{Token()}";
        var sessionId = $"cs_local_{Token()}";
        return Task.FromResult(new CustomerSetupSession(id, sessionId, successUrl, true));
    }

    public Task<SavedPaymentMethod> CompleteCustomerSetupAsync(string sessionId, string customerId) =>
        Task.FromResult(new SavedPaymentMethod($"pm_local_{Token()}", "card", "Visa", "4242"));

    public Task<ProviderOnboardingSession> CreateProviderOnboardingSessionAsync(
        string email, string? accountId, string returnUrl, string refreshUrl) =>
        Task.FromResult(new ProviderOnboardingSession(accountId ?? $"acct_local_{Token()}", returnUrl, true));

    public Task<ProviderPayoutState> GetProviderPayoutStateAsync(string accountId) =>
        Task.FromResult(new ProviderPayoutState(true, true, null));

    public Task<PaymentAuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request) =>
        Task.FromResult(new PaymentAuthorizationResult(
            LocalIntentPrefix + Token(), "requires_capture", DateTime.UtcNow.AddDays(7)));

    public Task<bool> CaptureAsync(string paymentIntentId, string idempotencyKey) => Task.FromResult(true);

    public Task<bool> CancelPaymentIntentAsync(string paymentIntentId) => Task.FromResult(true);

    public Task<bool> RefundPaymentIntentAsync(string paymentIntentId) => Task.FromResult(true);

    private static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
}
