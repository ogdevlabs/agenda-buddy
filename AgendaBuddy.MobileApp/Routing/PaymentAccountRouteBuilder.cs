namespace AgendaBuddy.MobileApp.Routing;

public static class PaymentAccountRouteBuilder
{
    public static RouteSpec Status() => new(HttpMethod.Get, "api/v1/booking/payments/account");
    public static RouteSpec BeginCustomerSetup() => new(HttpMethod.Post, "api/v1/booking/payments/customer/setup");
    public static RouteSpec CompleteCustomerSetup() => new(HttpMethod.Post, "api/v1/booking/payments/customer/setup/complete");
    public static RouteSpec BeginProviderOnboarding() => new(HttpMethod.Post, "api/v1/booking/payments/provider/onboarding");
}