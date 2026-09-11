namespace AgendaBuddy.MobileApp.Models;

public sealed record PaymentAccountStatus(
    string Role,
    bool IsReady,
    string Status,
    string? PaymentMethodType,
    string? PaymentMethodBrand,
    string? PaymentMethodLast4,
    bool CanSkipOnboarding = false);

public sealed record PaymentOnboardingLink(string Url, bool CompletedLocally);