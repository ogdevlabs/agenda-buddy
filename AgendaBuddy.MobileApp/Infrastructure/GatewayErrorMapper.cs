namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// Maps the gateway's <c>failedService</c> cluster id (api-contracts.md §1) to the human-readable
/// name the error banner shows — "booking" becomes "Booking", not the raw cluster id
/// (ux-review.md finding 2). Falls back to a generic message for a network error that never
/// reached the gateway, or an id this table doesn't recognize.
/// </summary>
public static class GatewayErrorMapper
{
    public const string GenericMessage = "Could not reach the server. Check your connection and try again.";

    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["booking"] = "Booking",
        ["calendar"] = "Calendar",
        ["customer"] = "Customers",
        ["provider"] = "Providers",
        ["services"] = "Services",
        ["profession"] = "Professions",
        ["identity"] = "Account",
    };

    public static string Describe(string? failedService)
    {
        if (string.IsNullOrWhiteSpace(failedService))
            return GenericMessage;

        return DisplayNames.TryGetValue(failedService, out var displayName)
            ? $"{displayName} is unavailable right now. Try again."
            : GenericMessage;
    }
}
