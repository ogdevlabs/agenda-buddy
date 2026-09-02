using System.Text;
using System.Text.Json;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

public class ProfessionApiService : IProfessionApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ProfessionApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Profession.Api does not register <see cref="Library.Tools.ObjectIdJsonConverter"/>
    /// (agenda-buddy-do5), so each element's <c>id</c> field is the broken multi-field BSON shape — reads
    /// <c>name</c> only and never touches <c>id</c>.
    /// </summary>
    public async Task<List<ProfessionItem>> GetProfessionsAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ProfessionRouteBuilder.Professions();
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<ProfessionItem>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseProfessions(json);
    }

    internal static List<ProfessionItem> ParseProfessions(string json)
    {
        var result = new List<ProfessionItem>();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var element in data.EnumerateArray())
        {
            if (element.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                result.Add(new ProfessionItem { Name = name.GetString() ?? string.Empty });
        }

        return result;
    }

    public async Task<List<string>> GetProviderProfessionsAsync(string email, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ProfessionRouteBuilder.GetProviderProfessions(email);
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<string>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseStringList(json);
    }

    public async Task<bool> AddProfessionsToProviderAsync(string email, List<string> professionNames, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ProfessionRouteBuilder.AddProfessionsToProvider(email);
        var body = JsonSerializer.Serialize(professionNames);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(route.Method, route.Path) { Content = content };
        var response = await client.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<ProfessionRemovalResult> RemoveProfessionFromProviderAsync(string email, string professionName, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = ProfessionRouteBuilder.RemoveProfessionFromProvider(email, professionName);
        var response = await client.DeleteAsync(route.Path, ct);

        if (response.IsSuccessStatusCode)
            return new ProfessionRemovalResult(true, null);

        var json = await response.Content.ReadAsStringAsync(ct);
        return new ProfessionRemovalResult(false, ParseFirstError(json));
    }

    private static List<string> ParseStringList(string json)
    {
        var result = new List<string>();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var element in data.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String)
                result.Add(element.GetString() ?? string.Empty);
        }

        return result;
    }

    private static string? ParseFirstError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.GetArrayLength() > 0)
                return errors[0].GetString();
        }
        catch (JsonException)
        {
            // Not every failure response body is JSON (e.g. a 404 with no body) -- treated as no message.
        }

        return null;
    }
}
