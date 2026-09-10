namespace AgendaBuddy.MobileApp.Infrastructure;

public enum PaymentOnboardingCallbackKind
{
    Unknown,
    CustomerCompleted,
    CustomerCancelled,
    ProviderCompleted,
    ProviderRefresh
}

public sealed record PaymentOnboardingCallback(PaymentOnboardingCallbackKind Kind, string? SessionId = null)
{
    public static PaymentOnboardingCallback Parse(Uri uri)
    {
        if (!string.Equals(uri.Scheme, "agendame", StringComparison.OrdinalIgnoreCase))
            return new(PaymentOnboardingCallbackKind.Unknown);

        var path = uri.AbsolutePath.Trim('/');
        if (string.Equals(uri.Host, "payment-method", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(path, "cancelled", StringComparison.OrdinalIgnoreCase))
                return new(PaymentOnboardingCallbackKind.CustomerCancelled);
            if (string.Equals(path, "complete", StringComparison.OrdinalIgnoreCase))
            {
                var sessionId = uri.Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Split('=', 2))
                    .Where(part => part.Length == 2)
                    .Where(part => string.Equals(part[0], "session_id", StringComparison.OrdinalIgnoreCase))
                    .Select(part => Uri.UnescapeDataString(part[1]))
                    .FirstOrDefault();
                return new(PaymentOnboardingCallbackKind.CustomerCompleted, sessionId);
            }
        }

        if (string.Equals(uri.Host, "provider-payout", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(path, "complete", StringComparison.OrdinalIgnoreCase))
                return new(PaymentOnboardingCallbackKind.ProviderCompleted);
            if (string.Equals(path, "refresh", StringComparison.OrdinalIgnoreCase))
                return new(PaymentOnboardingCallbackKind.ProviderRefresh);
        }

        return new(PaymentOnboardingCallbackKind.Unknown);
    }
}