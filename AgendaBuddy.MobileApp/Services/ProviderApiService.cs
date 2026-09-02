using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

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

    /// <summary>
    /// <c>GetAllProviders</c> answers <c>DataResponse&lt;PagedResponse&lt;ProviderSummary&gt;&gt;</c> —
    /// <c>{data: {items, totalCount, page, pageSize}, errors: []}</c> — the same nested-envelope shape
    /// <see cref="CustomerApiService.ParsePagedCustomers"/> already had to unwrap for
    /// <c>GET /api/v1/customers</c>. <c>ProviderSummary</c> has no <c>Id</c>/no id field at all (deliberately —
    /// see its own remarks), so unlike <see cref="GetReportAsync"/> this needs no <see cref="ObjectIdJsonConverter"/>
    /// dance.
    /// </summary>
    public async Task<List<CustomerSummary>> GetProvidersAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ProviderRouteBuilder.Providers();
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<CustomerSummary>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParsePagedProviders(json);
    }

    internal static List<CustomerSummary> ParsePagedProviders(string json)
    {
        var result = new List<CustomerSummary>();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var element in items.EnumerateArray())
        {
            var firstName = GetString(element, "firstName");
            var lastName = GetString(element, "lastName");
            var serviceNames = new List<string>();

            if (element.TryGetProperty("services", out var services) && services.ValueKind == JsonValueKind.Array)
            {
                foreach (var service in services.EnumerateArray())
                {
                    var name = GetString(service, "name");
                    if (!string.IsNullOrWhiteSpace(name))
                        serviceNames.Add(name);
                }
            }

            var professions = new List<string>();
            if (element.TryGetProperty("professions", out var professionsElement) && professionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var profession in professionsElement.EnumerateArray())
                {
                    var name = profession.ValueKind == JsonValueKind.String ? profession.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(name))
                        professions.Add(name);
                }
            }

            result.Add(new CustomerSummary
            {
                Email = GetString(element, "email"),
                FullName = string.Join(' ', new[] { firstName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s))),
                IsProvider = true,
                TotalSessions = serviceNames.Count,
                LastSession = serviceNames.Count > 0 ? string.Join(", ", serviceNames) : "No services listed yet",
                Availability = "Contact the provider to check availability",
                Professions = professions
            });
        }

        return result;
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

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
    public async Task<ProfileInfo?> GetProfileAsync(string email, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ProviderRouteBuilder.GetProvider(email);
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("data", out var data))
            return null;

        return new ProfileInfo
        {
            Email = data.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "",
            FirstName = data.TryGetProperty("firstName", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() ?? "" : "",
            LastName = data.TryGetProperty("lastName", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() ?? "" : "",
            PhoneNumber = data.TryGetProperty("phoneNumber", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : ""
        };
    }

    /// <summary>
    /// <c>PUT /api/v1/providers/{email}</c> REPLACES the whole stored document — <c>IRepository&lt;T&gt;</c>'s
    /// <c>FindOneAndUpdateAsync</c> is the only partial-update primitive in this codebase (ADR-032); every
    /// other write, including this one, replaces the entire entity. Sending only
    /// <c>{email, firstName, lastName}</c> would silently wipe <c>kafkaTopic</c>/<c>serviceEntities</c>/
    /// <c>appointmentEntities</c>/<c>subscribedCustomerCollection</c> — confirmed against the real backend
    /// during this feature's own end-to-end validation. This fetches the current full document, patches
    /// only the two edited fields in place, and PUTs the whole thing back.
    /// </summary>
    /// <summary>
    /// Creates the ProviderEntity that <c>POST api/v1/auth/register</c> does not. Called straight after a
    /// successful registration — until it exists the mandatory profession gate cannot be satisfied, because
    /// the repository never upserts and so the professions PUT has nothing to update.
    /// </summary>
    public async Task<bool> CreateProfileAsync(
        string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ProviderRouteBuilder.CreateProvider();
        var body = JsonSerializer.Serialize(
            ProviderRouteBuilder.BuildCreateProviderPayload(email, firstName, lastName, phoneNumber));
        var response = await client.PostAsync(route.Path, new StringContent(body, Encoding.UTF8, "application/json"), ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateProfileAsync(
        string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");

        var getResponse = await client.GetAsync(ProviderRouteBuilder.GetProvider(email).Path, ct);
        if (!getResponse.IsSuccessStatusCode)
            return false;

        var currentJson = await getResponse.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(currentJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("data", out var data))
            return false;

        var entity = JsonNode.Parse(data.GetRawText())!.AsObject();
        entity["firstName"] = firstName;
        entity["lastName"] = lastName;
        entity["phoneNumber"] = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber;

        // The device's zone IS the provider's working-hours zone. Recorded on every profile save so it
        // follows the device rather than needing to be picked from a list.
        entity["timeZoneId"] = TimeZoneInfo.Local.Id;
        // "id" round-trips fine here (Provider registers ObjectIdJsonConverter — see this file's other
        // remarks), so no field needs stripping before sending the merged document back.

        var route = ProviderRouteBuilder.UpdateProvider(email);
        var response = await client.PutAsync(route.Path,
            new StringContent(entity.ToJsonString(), Encoding.UTF8, "application/json"), ct);
        return response.IsSuccessStatusCode;
    }


    /// <summary>
    /// Records this device's timezone as the provider's, when it is not already what the server has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The provider's zone is what availability's 09:00–19:00 window is generated in, so a wrong or absent
    /// one offers slots at the wrong hours — a provider at UTC-6 with no zone recorded was offered
    /// 03:00–13:00 local. Taking it from the device means it is never asked for and follows a move.
    /// </para>
    /// <para>
    /// Silent by design: it is a background correction, not something to interrupt anyone about, and a
    /// failure just leaves the previous value in place. Returns whether anything was written.
    /// </para>
    /// </remarks>
    public async Task<bool> SyncTimeZoneAsync(string email, CancellationToken ct = default)
    {
        var deviceZone = TimeZoneInfo.Local.Id;
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");

        var getResponse = await client.GetAsync(ProviderRouteBuilder.GetProvider(email).Path, ct);
        if (!getResponse.IsSuccessStatusCode)
            return false;

        var currentJson = await getResponse.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(currentJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("data", out var data))
            return false;

        var entity = JsonNode.Parse(data.GetRawText())!.AsObject();

        // Nothing to do when it already matches -- this runs on every Account load, so it must not write
        // on every Account load.
        if (entity["timeZoneId"]?.GetValue<string>() == deviceZone)
            return false;

        entity["timeZoneId"] = deviceZone;

        var route = ProviderRouteBuilder.UpdateProvider(email);
        var response = await client.PutAsync(route.Path,
            new StringContent(entity.ToJsonString(), Encoding.UTF8, "application/json"), ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeactivateAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ProviderRouteBuilder.Deactivate(_session.Email);
        var response = await client.PostAsync(route.Path, null, ct);

        return response.IsSuccessStatusCode;
    }
}
