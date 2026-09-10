using System.Collections.Concurrent;

namespace AgendaBuddy.Library.Services;

/// <summary>Process-local email capture for simulator and local-development flows.</summary>
public sealed class LocalEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, LocalEmailMessage> _latest =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken cancellationToken = default) =>
        CaptureAsync(toAddress, subject, body, null);

    public Task<bool> SendAsync(
        string toAddress,
        string subject,
        string body,
        string htmlBody,
        CancellationToken cancellationToken = default) =>
        CaptureAsync(toAddress, subject, body, htmlBody);

    public LocalEmailMessage? LatestFor(string toAddress) =>
        _latest.GetValueOrDefault(toAddress);

    private Task<bool> CaptureAsync(string toAddress, string subject, string body, string? htmlBody)
    {
        if (string.IsNullOrWhiteSpace(toAddress)) return Task.FromResult(false);

        _latest[toAddress] = new LocalEmailMessage(
            toAddress,
            subject,
            body,
            htmlBody,
            DateTimeOffset.UtcNow);
        return Task.FromResult(true);
    }
}

public sealed record LocalEmailMessage(
    string ToAddress,
    string Subject,
    string Text,
    string? Html,
    DateTimeOffset CapturedAt);
