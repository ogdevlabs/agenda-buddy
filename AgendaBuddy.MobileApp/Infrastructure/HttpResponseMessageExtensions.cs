using System.Text.Json;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// Best-effort read of the gateway's <c>failedService</c> field (api-contracts.md §1) off a
/// non-success response. Returns <c>null</c> on any parse failure or an absent field — a plain
/// domain-service error (e.g. a 400 with its own ProblemDetails shape, or an empty body) is not a
/// gateway failure and callers fall back to a generic message for it, rather than crash on it.
/// </summary>
public static class HttpResponseMessageExtensions
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public static async Task<string?> TryReadFailedServiceAsync(
        this HttpResponseMessage response, CancellationToken ct = default)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var error = JsonSerializer.Deserialize<GatewayErrorResponse>(json, JsonOptions);
            return string.IsNullOrWhiteSpace(error?.FailedService) ? null : error!.FailedService;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
