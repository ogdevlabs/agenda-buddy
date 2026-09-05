using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgendaBuddy.Library.Services;

/// <summary>
/// The <see cref="IPushSender"/> registered when no push provider is configured: it logs that a message was
/// not delivered, names the key that would enable it, and returns <c>false</c>.
/// </summary>
/// <remarks>
/// <para>
/// The same shape as a non-charging <c>PaymentGatewayFactory</c> (ADR-038) and an API-key-less
/// <see cref="ResendEmailSender"/>: registered unconditionally so a local run needs no provider and callers
/// need no null check, and honest about having delivered nothing rather than reporting success.
/// </para>
/// <para>
/// <b>This is the whole push path today.</b> Nothing in this solution talks to FCM or APNs. The seam, the
/// device-token read and every call site are real and exercised; the provider call behind them is not
/// written, because FCM HTTP v1 needs a service-account credential and APNs needs an Apple push certificate,
/// and neither can be provisioned from here. Swapping this for an FCM implementation is a change to one
/// registration.
/// </para>
/// </remarks>
public class UnconfiguredPushSender(
    IOptions<PushOptions> options,
    ILogger<UnconfiguredPushSender>? logger = null) : IPushSender
{
    private readonly PushOptions _options = options.Value;

    public Task<bool> SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        // Names the key so the reason is actionable. Never the token or the body: the token identifies a
        // device and the body carries appointment detail, which is what PiiRedactingProcessor exists to keep
        // out of exported telemetry.
        logger?.LogWarning(
            "push.not-sent: no {Key} configured, so '{Title}' was not delivered",
            $"{PushOptions.Section}:{nameof(PushOptions.FirebaseProjectId)}", title);

        return Task.FromResult(false);
    }

    /// <summary>Whether a provider is configured at all. False here by definition.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.FirebaseProjectId)
        && !string.IsNullOrWhiteSpace(_options.ServiceAccountJson);
}
