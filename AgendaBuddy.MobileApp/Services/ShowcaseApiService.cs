using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

public class ShowcaseApiService : IShowcaseApiService
{
    public const int LookupMaxEmails = 50;

    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web);

    public ShowcaseApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Task<ShowcaseResult<MyShowcase>> GetMineAsync(CancellationToken ct = default) =>
        SendAsync<MyShowcase>(ShowcaseRouteBuilder.Mine(), null, ct);

    public Task<ShowcaseResult<MyShowcase>> SetTextAsync(string? tagline, string? about, CancellationToken ct = default) =>
        SendAsync<MyShowcase>(ShowcaseRouteBuilder.SetText(), new { tagline, about }, ct);

    public Task<ShowcaseResult<MyShowcase>> SetPhotoAsync(string? hash, CancellationToken ct = default) =>
        SendAsync<MyShowcase>(ShowcaseRouteBuilder.SetPhoto(), new { hash }, ct);

    public Task<ShowcaseResult<MyShowcase>> SetLogoAsync(string? hash, CancellationToken ct = default) =>
        SendAsync<MyShowcase>(ShowcaseRouteBuilder.SetLogo(), new { hash }, ct);

    public Task<ShowcaseResult<PortfolioItem>> AddPortfolioItemAsync(
        string hash, string? caption, string? serviceId, CancellationToken ct = default) =>
        SendAsync<PortfolioItem>(ShowcaseRouteBuilder.AddPortfolioItem(), new { hash, caption, serviceId }, ct);

    public Task<ShowcaseResult<PortfolioItem>> UpdatePortfolioItemAsync(
        string hash, string? caption, string? serviceId, CancellationToken ct = default) =>
        SendAsync<PortfolioItem>(ShowcaseRouteBuilder.UpdatePortfolioItem(hash), new { caption, serviceId }, ct);

    public Task<ShowcaseResult<bool>> RemovePortfolioItemAsync(string hash, CancellationToken ct = default) =>
        SendNoContentAsync(ShowcaseRouteBuilder.RemovePortfolioItem(hash), null, ct);

    public Task<ShowcaseResult<MyShowcase>> ReorderPortfolioAsync(
        IReadOnlyList<string> hashes, CancellationToken ct = default) =>
        SendAsync<MyShowcase>(ShowcaseRouteBuilder.ReorderPortfolio(), new { hashes }, ct);

    public Task<ShowcaseResult<ShowcasePublicCode>> GetPublicCodeAsync(CancellationToken ct = default) =>
        SendAsync<ShowcasePublicCode>(ShowcaseRouteBuilder.PublicCode(), null, ct);

    public Task<ShowcaseResult<ShowcaseView>> GetShowcaseAsync(
        string providerRef, ShowcaseSource source, CancellationToken ct = default) =>
        SendAsync<ShowcaseView>(ShowcaseRouteBuilder.View(providerRef, source), null, ct);

    public Task<ShowcaseResult<ShowcaseView>> GetShowcaseByCodeAsync(
        string code, ShowcaseSource source, CancellationToken ct = default) =>
        SendAsync<ShowcaseView>(ShowcaseRouteBuilder.ByCode(code, source), null, ct);

    public async Task<ShowcaseResult<List<ShowcaseLookupEntry>>> LookupAsync(
        IReadOnlyCollection<string> emails, CancellationToken ct = default)
    {
        var distinct = emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(LookupMaxEmails)
            .ToList();

        if (distinct.Count == 0)
            return ShowcaseResult<List<ShowcaseLookupEntry>>.Ok(new List<ShowcaseLookupEntry>());

        var result = await SendAsync<List<ShowcaseLookupEntry>>(ShowcaseRouteBuilder.Lookup(), new { emails = distinct }, ct);
        return result.IsSuccess && result.Value is null
            ? ShowcaseResult<List<ShowcaseLookupEntry>>.Ok(new List<ShowcaseLookupEntry>(), result.StatusCode)
            : result;
    }

    public Task<ShowcaseResult<bool>> ReportAsync(
        string providerRef, ShowcaseReportReason reason, string? detail, string? portfolioHash,
        CancellationToken ct = default) =>
        SendNoContentAsync(
            ShowcaseRouteBuilder.Report(providerRef),
            new { reason = ShowcaseReportReasons.WireValue(reason), detail, portfolioHash },
            ct);

    public Task<ShowcaseResult<bool>> HideAsync(string providerRef, CancellationToken ct = default) =>
        SendNoContentAsync(ShowcaseRouteBuilder.Hide(providerRef), null, ct);

    public Task<ShowcaseResult<bool>> UnhideAsync(string providerRef, CancellationToken ct = default) =>
        SendNoContentAsync(ShowcaseRouteBuilder.Unhide(providerRef), null, ct);

    public async Task<ShowcaseResult<List<HiddenProvider>>> GetHiddenAsync(CancellationToken ct = default)
    {
        var result = await SendAsync<List<HiddenProvider>>(ShowcaseRouteBuilder.Hidden(), null, ct);
        return result.IsSuccess && result.Value is null
            ? ShowcaseResult<List<HiddenProvider>>.Ok(new List<HiddenProvider>(), result.StatusCode)
            : result;
    }

    public async Task<ShowcaseResult<MediaUpload>> UploadImageAsync(byte[] jpeg, CancellationToken ct = default)
    {
        var content = new ByteArrayContent(jpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return await SendAsync<MediaUpload>(MediaRouteBuilder.Upload(), content, ct);
    }

    public async Task<ShowcaseResult<byte[]>> FetchImageAsync(
        string providerRef, string hash, MediaVariant variant, CancellationToken ct = default)
    {
        var route = MediaRouteBuilder.Fetch(providerRef, hash, variant);
        try
        {
            var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
            using var request = new HttpRequestMessage(route.Method, route.Path);
            using var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return ShowcaseResult<byte[]>.Ok(await response.Content.ReadAsByteArrayAsync(ct), (int)response.StatusCode);

            var body = await response.Content.ReadAsStringAsync(ct);
            return ShowcaseResult<byte[]>.Fail(
                ClassifyFailure(response.StatusCode, body), (int)response.StatusCode, ReadFailedService(body));
        }
        catch (Exception ex) when (IsTransport(ex, ct))
        {
            return ShowcaseResult<byte[]>.Fail(ShowcaseErrorCodes.Network, 0);
        }
    }

    private async Task<ShowcaseResult<T>> SendAsync<T>(RouteSpec route, object? body, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
            using var request = BuildRequest(route, body);
            using var response = await client.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return ShowcaseResult<T>.Fail(
                    ClassifyFailure(response.StatusCode, json), (int)response.StatusCode, ReadFailedService(json));

            return ShowcaseResult<T>.Ok(ReadData<T>(json)!, (int)response.StatusCode);
        }
        catch (JsonException)
        {
            return ShowcaseResult<T>.Fail(ShowcaseErrorCodes.Unknown, 200);
        }
        catch (Exception ex) when (IsTransport(ex, ct))
        {
            return ShowcaseResult<T>.Fail(ShowcaseErrorCodes.Network, 0);
        }
    }

    private async Task<ShowcaseResult<bool>> SendNoContentAsync(RouteSpec route, object? body, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
            using var request = BuildRequest(route, body);
            using var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return ShowcaseResult<bool>.Ok(true, (int)response.StatusCode);

            var json = await response.Content.ReadAsStringAsync(ct);
            return ShowcaseResult<bool>.Fail(
                ClassifyFailure(response.StatusCode, json), (int)response.StatusCode, ReadFailedService(json));
        }
        catch (Exception ex) when (IsTransport(ex, ct))
        {
            return ShowcaseResult<bool>.Fail(ShowcaseErrorCodes.Network, 0);
        }
    }

    private static HttpRequestMessage BuildRequest(RouteSpec route, object? body)
    {
        var request = new HttpRequestMessage(route.Method, route.Path);
        request.Content = body switch
        {
            null => null,
            HttpContent content => content,
            _ => new StringContent(JsonSerializer.Serialize(body, WriteOptions), Encoding.UTF8, "application/json")
        };
        return request;
    }

    private static bool IsTransport(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested);

    /// <summary>
    /// Reads the <c>data</c> member of the <c>{success, data, errors}</c> envelope, or the body itself when a
    /// route answers a bare value.
    /// </summary>
    public static T? ReadData<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && TryGetProperty(root, "data", out var data))
            return data.ValueKind == JsonValueKind.Null ? default : data.Deserialize<T>(JsonOptions);

        return root.Deserialize<T>(JsonOptions);
    }

    /// <summary>
    /// Turns a failed response into one of <see cref="ShowcaseErrorCodes"/>. The server's own code wins wherever it
    /// sent one — the <c>code</c> extension on a 409, the <c>type</c> on a 404, the entries under
    /// <c>errors.image</c>/<c>errors.hash</c> on a 400 — and the status decides only when it did not.
    /// </summary>
    public static string ClassifyFailure(HttpStatusCode status, string? body)
    {
        var codes = ReadCodes(body);
        var known = codes.FirstOrDefault();

        return status switch
        {
            HttpStatusCode.TooManyRequests => ShowcaseErrorCodes.RateLimited,
            HttpStatusCode.RequestEntityTooLarge => ShowcaseErrorCodes.TooLarge,
            HttpStatusCode.NotFound => ShowcaseErrorCodes.ShowcaseNotFound,
            HttpStatusCode.ServiceUnavailable when known is null => ShowcaseErrorCodes.StorageUnavailable,
            HttpStatusCode.BadRequest when known is null => ShowcaseErrorCodes.Invalid,
            _ => known ?? ShowcaseErrorCodes.Unknown
        };
    }

    private static List<string> ReadCodes(string? body)
    {
        var codes = new List<string>();
        if (string.IsNullOrWhiteSpace(body))
            return codes;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return codes;

            AddString(root, "code", codes);
            AddString(root, "type", codes);

            if (TryGetProperty(root, "errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errors.EnumerateObject())
                {
                    if (field.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var entry in field.Value.EnumerateArray())
                            AddCodesFromText(entry.ValueKind == JsonValueKind.String ? entry.GetString() : null, codes);
                    }
                    else if (field.Value.ValueKind == JsonValueKind.String)
                    {
                        AddCodesFromText(field.Value.GetString(), codes);
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return codes;
    }

    private static void AddString(JsonElement root, string name, List<string> codes)
    {
        if (TryGetProperty(root, name, out var value) && value.ValueKind == JsonValueKind.String)
            AddCodesFromText(value.GetString(), codes);
    }

    // An entry may be the bare code, a type URI ending in it, or "code: message" — every one of those names the code.
    private static void AddCodesFromText(string? text, List<string> codes)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        foreach (var code in ShowcaseErrorCodes.Server)
        {
            if (text.Contains(code, StringComparison.OrdinalIgnoreCase))
                codes.Add(code);
        }
    }

    private static string? ReadFailedService(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            var error = JsonSerializer.Deserialize<GatewayErrorResponse>(body, JsonOptions);
            return string.IsNullOrWhiteSpace(error?.FailedService) ? null : error!.FailedService;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
