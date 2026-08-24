using System.Net.Http.Json;
using System.Text.Json;
using MobileApp.Models;
using MobileApp.Routing;

namespace MobileApp.Services;

public class CustomerApiService : ICustomerApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public CustomerApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<CustomerSummary>> GetCustomersAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CustomerRouteBuilder.Customers();
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<CustomerSummary>();

        var result = await response.Content.ReadFromJsonAsync<List<CustomerSummary>>(
            _jsonOptions, cancellationToken: ct);

        return result ?? new List<CustomerSummary>();
    }
}
