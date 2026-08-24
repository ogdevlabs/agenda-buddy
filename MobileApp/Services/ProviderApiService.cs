using System.Text.Json;
using Library.Entities;
using MobileApp.Infrastructure;
using MobileApp.Routing;

namespace MobileApp.Services;

public class ProviderApiService : IProviderApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUserSessionService _session;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public ProviderApiService(IHttpClientFactory httpClientFactory, IUserSessionService session)
    {
        _httpClientFactory = httpClientFactory;
        _session = session;
    }

    public async Task<ProviderReport?> GetReportAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ProviderRouteBuilder.Report(_session.Email);
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
        {
            var failedService = await response.TryReadFailedServiceAsync(ct);
            if (failedService is not null)
                throw new GatewayServiceUnavailableException(failedService);

            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<ProviderReport>(json, JsonOptions);
    }

    // The real route's success body is ambiguously typed in Provider/Program.cs (declared
    // Results<Accepted<string>, …> but constructed via TypedResults.Accepted(location, ProviderEntity) —
    // a genuine mismatch in the backend's own type union). Rather than bind that body into a client shape
    // that may not match either declared or actual type, this reports success/failure only — the caller
    // (deactivation is a one-way action with no detail screen to refresh) does not need the body.
    public async Task<bool> DeactivateAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ProviderRouteBuilder.Deactivate(_session.Email);
        var response = await client.PostAsync(route.Path, null, ct);

        return response.IsSuccessStatusCode;
    }
}
