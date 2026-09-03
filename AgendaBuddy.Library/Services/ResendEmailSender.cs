using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgendaBuddy.Library.Services;

/// <summary>
/// Sends through Resend's HTTP API. One POST, no SDK.
/// </summary>
/// <remarks>
/// <para>
/// With no API key configured this becomes a no-op that logs and returns <c>false</c>. That keeps a local
/// AppHost run working without an outbound dependency, and it is deliberately not a throw: registration
/// must not fail because a developer has no mail provider.
/// </para>
/// <para>
/// It never logs the message body, because the body is where the token is. The reason the tokens were being
/// written to the log in the first place was that there was nowhere else for them to go; putting them back
/// in via a delivery log would undo the fix.
/// </para>
/// </remarks>
public class ResendEmailSender(
    IHttpClientFactory httpClientFactory,
    IOptions<EmailOptions> options,
    ILogger<ResendEmailSender>? logger = null) : IEmailSender
{
    public const string HttpClientName = "Resend";

    private const string SendEndpoint = "https://api.resend.com/emails";

    private readonly EmailOptions _options = options.Value;

    public async Task<bool> SendAsync(
        string toAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            // Names the key so the reason is actionable, and says nothing about the contents.
            logger?.LogWarning(
                "email.not-sent: no {Key} configured, so '{Subject}' was not delivered",
                $"{EmailOptions.Section}:ApiKey", subject);
            return false;
        }

        if (string.IsNullOrWhiteSpace(toAddress)) return false;

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint)
            {
                Content = JsonContent.Create(new
                {
                    from = $"{_options.FromName} <{_options.FromAddress}>",
                    to = new[] { toAddress },
                    subject,
                    text = body
                })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", _options.ApiKey);

            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode) return true;

            // Status only. A Resend error body echoes the request, which includes the token.
            logger?.LogWarning(
                "email.send-failed: provider answered {StatusCode} for '{Subject}'",
                (int)response.StatusCode, subject);
            return false;
        }
        catch (Exception ex)
        {
            // Swallowed by contract (see IEmailSender): a delivery failure must not fail the operation
            // that triggered it, and on the reset path a 500 would confirm the address exists.
            logger?.LogWarning(ex, "email.send-failed: could not reach the email provider for '{Subject}'", subject);
            return false;
        }
    }
}
