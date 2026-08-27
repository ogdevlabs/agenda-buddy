using System.Text.Json;
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

    /// <summary>
    /// Customer's list route returns F-016/ADR-023's <c>{items, totalCount, page, pageSize}</c> envelope of
    /// full <c>CustomerEntity</c> objects, not a bare array of the client's <see cref="CustomerSummary"/>
    /// shape — deserializing straight into <c>List&lt;CustomerSummary&gt;</c> would fail against the
    /// object root. Reads the envelope, maps only the fields <c>CustomerEntity</c> actually has
    /// (Email, FirstName/LastName); the rest of <see cref="CustomerSummary"/> (Phone, LastSession,
    /// TotalSessions, Availability) has no backend equivalent and stays at its default — a data-shape gap
    /// out of this task's scope (route/verb/payload correctness only).
    /// </summary>
    internal static List<CustomerSummary> ParsePagedCustomers(string json)
    {
        var result = new List<CustomerSummary>();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("items", out var items)
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
                FullName = string.Join(' ', new[] { firstName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s)))
            });
        }

        return result;
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
