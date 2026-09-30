using AgendaBuddy.MobileApp.Infrastructure;

namespace AgendaBuddy.MobileApp.Services;

public enum ShareOutcome
{
    Shared,

    /// <summary>The share sheet was dismissed. Not an error, and never reported as one.</summary>
    Cancelled,

    Failed
}

/// <summary>Draws a share image for the provider's code and opens the system share sheet with it.</summary>
public interface IShareImageService
{
    Task<ShareOutcome> ShareAsync(ShareFormat format, ShareCardText text, string qrPayload, CancellationToken ct = default);
}

public sealed class UnavailableShareImageService : IShareImageService
{
    public Task<ShareOutcome> ShareAsync(
        ShareFormat format, ShareCardText text, string qrPayload, CancellationToken ct = default) =>
        Task.FromResult(ShareOutcome.Failed);
}
