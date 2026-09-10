using System.Net.Http.Json;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

public class PaymentAccountApiService(IHttpClientFactory httpClientFactory) : IPaymentAccountApiService
{
    public async Task<PaymentAccountStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("AgendaBuddyApi");
        var response = await client.GetAsync(PaymentAccountRouteBuilder.Status().Path, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PaymentAccountStatus>(cancellationToken);
    }

    public Task<PaymentOnboardingLink?> BeginCustomerSetupAsync(CancellationToken cancellationToken = default) =>
        BeginAsync(PaymentAccountRouteBuilder.BeginCustomerSetup(), cancellationToken);

    public Task<PaymentOnboardingLink?> BeginProviderOnboardingAsync(CancellationToken cancellationToken = default) =>
        BeginAsync(PaymentAccountRouteBuilder.BeginProviderOnboarding(), cancellationToken);

    public async Task<PaymentAccountStatus?> CompleteCustomerSetupAsync(
        string sessionId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = PaymentAccountRouteBuilder.CompleteCustomerSetup();
        var response = await client.PostAsJsonAsync(route.Path, new { sessionId }, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PaymentAccountStatus>(cancellationToken);
    }

    private async Task<PaymentOnboardingLink?> BeginAsync(
        RouteSpec route, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("AgendaBuddyApi");
        var response = await client.PostAsync(route.Path, null, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PaymentOnboardingLink>(cancellationToken);
    }
}