using AgendaBuddy.MobileApp.Resources.Strings;

namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// Maps the gateway's <c>failedService</c> cluster id (api-contracts.md §1) to the human-readable
/// name the error banner shows — "booking" becomes "Booking", not the raw cluster id
/// (ux-review.md finding 2). Falls back to a generic message for a network error that never
/// reached the gateway, or an id this table doesn't recognize.
/// </summary>
public static class GatewayErrorMapper
{
    public static string GenericMessage => AppResources.GetString("Gateway_GenericError");

    public static string Describe(string? failedService)
    {
        if (string.IsNullOrWhiteSpace(failedService))
            return GenericMessage;

        var displayName = RuntimeText.GatewayService(failedService);
        return string.IsNullOrEmpty(displayName)
            ? GenericMessage
            : AppResources.Format("Gateway_ServiceUnavailable", displayName);
    }
}
