using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

public class CustomerApiService : ICustomerApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

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

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParsePagedCustomers(json);
    }

    public async Task<bool> SubscribeAsync(string customerEmail, string providerEmail, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CustomerRouteBuilder.Subscribe(customerEmail, providerEmail);
        var response = await client.PostAsync(route.Path, null, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnsubscribeAsync(string customerEmail, string providerEmail, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CustomerRouteBuilder.Unsubscribe(customerEmail, providerEmail);
        var response = await client.DeleteAsync(route.Path, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<ProfileInfo?> GetProfileAsync(string email, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CustomerRouteBuilder.GetCustomer(email);
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("data", out var data))
            return null;

        return new ProfileInfo
        {
            Email = GetString(data, "email"),
            FirstName = GetString(data, "firstName"),
            LastName = GetString(data, "lastName"),
            PhoneNumber = GetString(data, "phoneNumber")
        };
    }

    /// <summary>
    /// Creates the CustomerEntity that <c>POST api/v1/auth/register</c> does not. Called straight after a
    /// successful registration.
    /// </summary>
    public async Task<bool> CreateProfileAsync(
        string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CustomerRouteBuilder.CreateCustomer();
        var body = JsonSerializer.Serialize(
            CustomerRouteBuilder.BuildCreateCustomerPayload(email, firstName, lastName, phoneNumber));
        var response = await client.PostAsync(route.Path, new StringContent(body, Encoding.UTF8, "application/json"), ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Fetch-merge-PUT, matching the Provider path. <c>PUT api/v1/customers/{email}</c> is a whole-document
    /// replace, so sending only the edited fields silently dropped everything else on the record —
    /// phone number, subscribedProviderCollection and appointmentCollection all went with it.
    /// </summary>
    public async Task<bool> UpdateProfileAsync(
        string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");

        var getResponse = await client.GetAsync(CustomerRouteBuilder.GetCustomer(email).Path, ct);
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

        // Customer.Api does not register ObjectIdJsonConverter (agenda-buddy-do5), so "id" arrives as the
        // broken multi-field BSON shape and cannot be sent back — strip it and let the server match on the
        // route's email, which is what it keys the replace on anyway.
        entity.Remove("id");

        var route = CustomerRouteBuilder.UpdateCustomer(email);
        var response = await client.PutAsync(route.Path,
            new StringContent(entity.ToJsonString(), Encoding.UTF8, "application/json"), ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<string>> GetSubscriptionsAsync(string customerEmail, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CustomerRouteBuilder.Subscriptions(customerEmail);
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<string>();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return data.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? string.Empty)
            .ToList();
    }

    /// <summary>
    /// Customer's list route returns ADR-023's <c>{items, totalCount, page, pageSize}</c> envelope of
    /// full <c>CustomerEntity</c> objects, not a bare array of the client's <see cref="CustomerSummary"/>
    /// shape — deserializing straight into <c>List&lt;CustomerSummary&gt;</c> would fail against the
    /// object root. Reads the envelope, maps only the fields <c>CustomerEntity</c> actually has
    /// (Email, FirstName/LastName); the rest of <see cref="CustomerSummary"/> (Phone, LastSession,
    /// TotalSessions, Availability) has no backend equivalent and stays at its default — a data-shape gap
    /// out of this task's scope (route/verb/payload correctness only).
    /// </summary>
    /// <remarks>
    /// The paged envelope itself sits one level deeper, under a "data" property
    /// (<c>{data: {items, totalCount, page, pageSize}, errors: []}</c>) — Customer's Clean Architecture
    /// wraps every CQRS route's response in <c>DataResponse&lt;T&gt;</c> (ADR-049).
    /// </remarks>
    internal static List<CustomerSummary> ParsePagedCustomers(string json)
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

            result.Add(new CustomerSummary
            {
                Id = GetString(element, "id"),
                Email = GetString(element, "email"),
                FullName = string.Join(' ', new[] { firstName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Phone = GetString(element, "phoneNumber")
            });
        }

        return result;
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
