using Microsoft.Extensions.Configuration;

namespace Library.Services;

/// <summary>Which <see cref="IPaymentGateway"/> implementation a configuration selects.</summary>
public enum PaymentGatewayMode
{
    /// <summary>Records payments locally and contacts nothing. The default.</summary>
    Recording,

    /// <summary>Talks to Stripe. Selected only when an API key is configured.</summary>
    Stripe
}

/// <summary>
/// Chooses a payment gateway from configuration, and says so out loud when the choice is the
/// non-charging one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Non-charging by default</b> (F-014 requirement 17, ADR D-6). There is no Stripe account, no key and
/// no deployment — ADR-035 defers cloud until every pending feature ships. The two alternatives both fail:
/// a gateway that throws leaves <c>PaymentService</c> unreachable, which is the exact condition F-014
/// exists to end; a gateway that charges by default is unthinkable without an account. Recording locally
/// is the only option that leaves the capability exercisable and the money untouched.
/// </para>
/// <para>
/// <b>Why this is a factory rather than a <c>IServiceCollection</c> extension.</b> <c>Library</c> does not
/// reference <c>Microsoft.Extensions.DependencyInjection.Abstractions</c>, and adding it to widen this
/// project's dependency surface for one <c>AddSingleton</c> is a poor trade. The <i>decision</i> lives here
/// where it can be unit-tested; the registration lives at the call site that consumes it.
/// </para>
/// </remarks>
public static class PaymentGatewayFactory
{
    /// <summary>
    /// The configuration key holding the Stripe secret. <b>It must never appear in
    /// <c>appsettings.json</c></b> — it is a live payment credential, and `ISSUE-002` is this project's
    /// standing proof that a committed secret is permanent. It follows the JWT keys: an Aspire secret
    /// parameter, prompted once, masked in the dashboard (threat T-206).
    /// </summary>
    public const string ApiKeyConfigurationKey = "Payments:Stripe:ApiKey";

    /// <summary>Which gateway <paramref name="configuration"/> selects.</summary>
    public static PaymentGatewayMode ModeFor(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return string.IsNullOrWhiteSpace(configuration[ApiKeyConfigurationKey])
            ? PaymentGatewayMode.Recording
            : PaymentGatewayMode.Stripe;
    }

    /// <summary>Builds the gateway <paramref name="configuration"/> selects.</summary>
    public static IPaymentGateway Create(IConfiguration configuration) =>
        ModeFor(configuration) switch
        {
            PaymentGatewayMode.Stripe => new StripePaymentGateway(configuration[ApiKeyConfigurationKey]!),
            _ => new RecordingPaymentGateway()
        };

    /// <summary>
    /// A warning to log at startup when payments are not real and this does not look like a local run, or
    /// <c>null</c> when there is nothing to say.
    /// </summary>
    /// <remarks>
    /// PRD risk R4: the residual risk of a non-charging default is that it becomes permanent — a deployment
    /// forgets the key and records payments that never happened, while every artifact says F-010 is
    /// delivered. This is the same shape as threat T-103, and it gets the same mitigation F-021 chose
    /// (ADR-033): warn loudly, naming the key, rather than failing to start. A missing payment key should
    /// not take down appointment booking.
    /// </remarks>
    public static string? RecordingModeWarning(IConfiguration configuration, bool isLocalRun)
    {
        if (isLocalRun || ModeFor(configuration) is not PaymentGatewayMode.Recording) return null;

        return $"Payments are NOT REAL: no {ApiKeyConfigurationKey} is configured, so charges are recorded "
               + "locally with a 'local_' intent id and no external call is made. Every payment will read "
               + "as Succeeded without money moving.";
    }
}
