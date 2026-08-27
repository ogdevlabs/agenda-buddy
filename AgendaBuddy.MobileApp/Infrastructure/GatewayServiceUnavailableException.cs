namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// Thrown when an <c>*ApiService</c> call gets a non-success response that carries the gateway's
/// <c>failedService</c> field (api-contracts.md §1) — i.e. a specific backend cluster is down, not a
/// generic network failure. ViewModels catch this and map <see cref="FailedService"/> through
/// <see cref="GatewayErrorMapper"/> for the error banner (ux-review.md finding 2).
/// </summary>
public class GatewayServiceUnavailableException : Exception
{
    public string FailedService { get; }

    public GatewayServiceUnavailableException(string failedService)
        : base($"The '{failedService}' service did not respond.")
    {
        FailedService = failedService;
    }
}
