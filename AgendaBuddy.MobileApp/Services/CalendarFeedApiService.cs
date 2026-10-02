using System.Text;
using System.Text.Json;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

/// <inheritdoc cref="ICalendarFeedApiService"/>
public class CalendarFeedApiService(IHttpClientFactory httpClientFactory) : ICalendarFeedApiService
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web);

    public async Task<CalendarFeedStatus?> GetStatusAsync(CancellationToken ct = default)
    {
        var json = await SendAsync(CalendarFeedRouteBuilder.Status(), null, ct);
        return json is null ? null : ReadData<CalendarFeedStatus>(json);
    }

    public async Task<CalendarFeedLink?> EnableAsync(string language, CancellationToken ct = default)
    {
        var json = await SendAsync(
            CalendarFeedRouteBuilder.Enable(), CalendarFeedRouteBuilder.BuildEnablePayload(language), ct);
        var link = json is null ? null : ReadData<CalendarFeedLink>(json);
        return string.IsNullOrWhiteSpace(link?.Url) ? null : link;
    }

    public async Task<bool> DisableAsync(CancellationToken ct = default) =>
        await SendAsync(CalendarFeedRouteBuilder.Disable(), null, ct) is not null;

    private async Task<string?> SendAsync(RouteSpec route, object? body, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("AgendaBuddyApi");
            using var request = new HttpRequestMessage(route.Method, route.Path);
            if (body is not null)
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body, WriteOptions), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, ct);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : null;
        }
        catch (Exception exception) when (exception is HttpRequestException
                                             or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>Reads the <c>data</c> member of the response envelope; <c>null</c> when the body is not readable.</summary>
    public static T? ReadData<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
                root = data;
            return root.ValueKind == JsonValueKind.Object ? root.Deserialize<T>(ReadOptions) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
