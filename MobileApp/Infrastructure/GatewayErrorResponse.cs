namespace MobileApp.Infrastructure;

/// <summary>
/// The gateway's ProblemDetails-shaped error body for a destination failure
/// (docs/pdlc/design/api-gateway-and-mobile-contract/api-contracts.md §1). Only <c>failedService</c>
/// is bound here — the client needs nothing else from this shape today.
/// </summary>
public class GatewayErrorResponse
{
    public string? FailedService { get; set; }
}
