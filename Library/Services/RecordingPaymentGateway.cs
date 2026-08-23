using System.Security.Cryptography;

namespace Library.Services;

/// <summary>
/// An <see cref="IPaymentGateway"/> that records a payment locally and contacts nothing.
/// </summary>
/// <remarks>
/// <para>
/// F-014's default gateway (requirement 17, ADR D-6). It exists so that <c>PaymentService</c> — one of the
/// six capabilities this feature makes reachable — can be exercised by a developer and by a test without a
/// Stripe account, a live secret key, or any possibility of moving money.
/// </para>
/// <para>
/// <b>It reports success.</b> That is deliberate and it is the risk: a caller cannot tell a recorded
/// payment from a settled one by its <c>status</c> alone. The signal is the intent id, which is prefixed
/// <see cref="LocalIntentPrefix"/> — a value Stripe cannot produce. `api-contracts.md` §2 states this in the
/// contract rather than leaving a client to infer it, and
/// <see cref="PaymentGatewayFactory.RecordingModeWarning"/> says it at startup outside a local run.
/// </para>
/// <para>
/// The alternative — failing every charge — would leave the capability unreachable, which is the exact
/// condition F-014 exists to end, and would make PRD AC-6 unwritable.
/// </para>
/// </remarks>
public sealed class RecordingPaymentGateway : IPaymentGateway
{
    /// <summary>
    /// Marks an intent id as locally generated. Stripe's ids begin <c>pi_</c>, so this cannot collide with a
    /// real one, and its presence in stored data is a permanent record that the payment was never charged.
    /// </summary>
    public const string LocalIntentPrefix = "local_";

    public Task<string> CreatePaymentIntentAsync(decimal amount, string currency, string description) =>
        Task.FromResult(LocalIntentPrefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant());

    public Task<bool> ConfirmPaymentIntentAsync(string paymentIntentId) => Task.FromResult(true);

    public Task<bool> RefundPaymentIntentAsync(string paymentIntentId) => Task.FromResult(true);
}
