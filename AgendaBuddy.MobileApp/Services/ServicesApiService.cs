using System.Text;
using System.Text.Json;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

public class ServicesApiService : IServicesApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ServicesApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Services.Api does not register <see cref="Library.Tools.ObjectIdJsonConverter"/> (agenda-buddy-do5),
    /// so each element's <c>id</c> field is the broken multi-field BSON shape — this reads
    /// name/description/fee/feeType/isActive field-by-field and never touches <c>id</c>, matching
    /// <see cref="CalendarApiService.ParseAppointments"/>'s established workaround for the same gap.
    /// </summary>
    public async Task<List<ServiceItem>> GetServicesAsync(string email, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ServicesRouteBuilder.GetServices(email);
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<ServiceItem>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseServices(json);
    }

    internal static List<ServiceItem> ParseServices(string json)
    {
        var result = new List<ServiceItem>();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var element in data.EnumerateArray())
        {
            result.Add(new ServiceItem
            {
                Name = GetString(element, "name"),
                Description = GetString(element, "description"),
                Fee = GetDecimal(element, "fee"),
                FeeType = GetFeeType(element),
                IsActive = !element.TryGetProperty("isActive", out var active) || active.ValueKind != JsonValueKind.False,
                DurationMinutes = GetInt(element, "durationMinutes"),
                ProfessionName = element.TryGetProperty("professionName", out var profession) && profession.ValueKind == JsonValueKind.String
                    ? profession.GetString()
                    : null
            });
        }

        return result;
    }

    public async Task<bool> AddServicesAsync(string email, List<ServiceItem> newServices, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ServicesRouteBuilder.AddServices(email);
        var response = await SendServicesAsync(client, route, newServices, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateServicesAsync(string email, List<ServiceItem> updatedServices, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ServicesRouteBuilder.UpdateServices(email);
        var response = await SendServicesAsync(client, route, updatedServices, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveServiceAsync(string email, string name, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ServicesRouteBuilder.RemoveService(email, name);
        var response = await client.DeleteAsync(route.Path, ct);
        return response.IsSuccessStatusCode;
    }

    private static async Task<HttpResponseMessage> SendServicesAsync(
        HttpClient client, RouteSpec route, List<ServiceItem> serviceItems, CancellationToken ct)
    {
        var payload = serviceItems
            .Select(s => ServicesRouteBuilder.BuildServicePayload(s.Name, s.Description, s.Fee, s.FeeType, s.IsActive, s.DurationMinutes, s.ProfessionName))
            .ToList();
        var body = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(route.Method, route.Path) { Content = content };
        return await client.SendAsync(request, ct);
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    // JsonElement.TryGetDecimal/TryGetInt32 throw InvalidOperationException for a JSON `null` value
    // (ValueKind.Null) rather than returning false -- so a field that is explicitly null on the wire
    // (durationMinutes is documented as exactly that, ServiceItem.cs's own remarks) needs its ValueKind
    // checked first, the same guard GetFeeType already uses.
    private static decimal? GetDecimal(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var d)
            ? d
            : null;

    private static int? GetInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i)
            ? i
            : null;

    private static FeeType GetFeeType(JsonElement element)
    {
        if (!element.TryGetProperty("feeType", out var value))
            return FeeType.Fixed;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric) && Enum.IsDefined(typeof(FeeType), numeric))
            return (FeeType)numeric;

        if (value.ValueKind == JsonValueKind.String && Enum.TryParse<FeeType>(value.GetString(), ignoreCase: true, out var parsed))
            return parsed;

        return FeeType.Fixed;
    }
}
