using System.Text;
using System.Text.Json;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

/// <inheritdoc cref="ICalendarBlockApiService"/>
public class CalendarBlockApiService(
    IHttpClientFactory httpClientFactory,
    IUserSessionService session) : ICalendarBlockApiService
{
    public async Task<List<CalendarBlock>> GetBlocksAsync(CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CalendarBlockRouteBuilder.GetBlocks(session.Email);

        var response = await client.GetAsync(route.Path, ct);

        // Raised rather than swallowed into an empty list: an empty list means "no time off recorded", and
        // showing that for a failed request tells the provider their calendar is open when it may not be.
        if (!response.IsSuccessStatusCode)
        {
            var failedService = await response.TryReadFailedServiceAsync(ct);
            if (failedService is not null) throw new GatewayServiceUnavailableException(failedService);
            response.EnsureSuccessStatusCode();
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data)) root = data;
        if (root.ValueKind != JsonValueKind.Array) return [];

        var blocks = new List<CalendarBlock>();

        foreach (var element in root.EnumerateArray())
        {
            var identifier = ReadString(element, "identifier");
            if (string.IsNullOrWhiteSpace(identifier)) continue;

            if (!TryReadUtc(element, "start", out var start) || !TryReadUtc(element, "end", out var end))
                continue;

            // Rendered on the provider's own clock. Stored UTC, so the conversion happens once, here.
            blocks.Add(new CalendarBlock(
                identifier, start.ToLocalTime(), end.ToLocalTime(), ReadString(element, "reason")));
        }

        return [.. blocks.OrderBy(block => block.Start)];
    }

    public async Task<int?> CountConflictsAsync(
        DateTime startLocal, DateTime endLocal, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CalendarBlockRouteBuilder.BlockConflicts(
            session.Email, startLocal.ToUniversalTime(), endLocal.ToUniversalTime());

        try
        {
            var response = await client.GetAsync(route.Path, ct);
            if (!response.IsSuccessStatusCode) return null;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data)) root = data;

            return root.ValueKind == JsonValueKind.Array ? root.GetArrayLength() : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                             or JsonException)
        {
            // null, never 0: "could not ask" and "nothing in the way" must not arrive worded the same, or the
            // provider is told their range is clear on a dropped connection.
            return null;
        }
    }

    public async Task<AppointmentActionResult> BlockAsync(
        DateTime startLocal, DateTime endLocal, string? reason, bool force = false,
        CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CalendarBlockRouteBuilder.CreateBlock(session.Email);

        // Converted once, here. The provider picks in their own clock; the server stores instants.
        var body = JsonSerializer.Serialize(CalendarBlockRouteBuilder.BuildBlockPayload(
            startLocal.ToUniversalTime(), endLocal.ToUniversalTime(), reason, force));

        return await SendAsync(
            client, new HttpRequestMessage(HttpMethod.Post, route.Path)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            }, ct);
    }

    public async Task<AppointmentActionResult> RemoveBlockAsync(
        string identifier, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CalendarBlockRouteBuilder.RemoveBlock(session.Email, identifier);

        return await SendAsync(client, new HttpRequestMessage(HttpMethod.Delete, route.Path), ct);
    }

    /// <summary>
    /// Sends and words the outcome, preferring the server's own message.
    /// </summary>
    /// <remarks>
    /// The refusal that matters is the `409` naming how many sessions fall inside the range — a count only the
    /// server holds, and the whole reason a bool would not do here.
    /// </remarks>
    private static async Task<AppointmentActionResult> SendAsync(
        HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        using (request)
        {
            try
            {
                var response = await client.SendAsync(request, ct);
                if (response.IsSuccessStatusCode) return AppointmentActionResult.Done();

                var failedService = await response.TryReadFailedServiceAsync(ct);
                if (failedService is not null)
                    return new AppointmentActionResult(false, GatewayErrorMapper.Describe(failedService));

                return AppointmentActionResult.Refused(
                    response.StatusCode, await ReadMessageAsync(response, ct));
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return AppointmentActionResult.Unreachable();
            }
        }
    }

    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(raw)) return null;

            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!document.RootElement.TryGetProperty("errors", out var errors)) return null;

            var messages = errors.ValueKind switch
            {
                JsonValueKind.Array => errors.EnumerateArray()
                    .Where(error => error.ValueKind == JsonValueKind.String)
                    .Select(error => error.GetString()),
                JsonValueKind.Object => errors.EnumerateObject()
                    .SelectMany(field => field.Value.ValueKind == JsonValueKind.Array
                        ? field.Value.EnumerateArray().Select(item => item.GetString())
                        : [field.Value.GetString()]),
                _ => []
            };

            var joined = string.Join(" ", messages.Where(message => !string.IsNullOrWhiteSpace(message)));
            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool TryReadUtc(JsonElement element, string propertyName, out DateTime value)
    {
        // ValueKind first: TryGetDateTime THROWS on a JSON null rather than returning false, despite the name.
        if (element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && property.TryGetDateTime(out var parsed))
        {
            value = parsed.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : parsed;
            return true;
        }

        value = default;
        return false;
    }
}
