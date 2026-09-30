using System.Net;
using System.Text;
using System.Text.Json;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;
using AgendaBuddy.MobileApp.Services;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

public class ShowcaseApiServiceTests
{
    private const string MineBody =
        """{ "success": true, "data": { "providerRef": "66f1", "portfolio": [], "completeness": { "done": 0, "total": 5, "missing": [] }, "funnel": { "scans": 0, "opened": 0, "booked": 0, "windowDays": 7 } } }""";

    private static (ShowcaseApiService Service, RecordingHandler Handler) Create(
        HttpStatusCode status, string? body = null, string contentType = "application/json")
    {
        var handler = new RecordingHandler(() => new HttpResponseMessage(status)
        {
            Content = new StringContent(body ?? string.Empty, Encoding.UTF8, contentType)
        });
        return (new ShowcaseApiService(Factory(handler)), handler);
    }

    private static IHttpClientFactory Factory(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApi")).Returns(client);
        return factory.Object;
    }

    private static JsonElement BodyOf(RecordingHandler handler) =>
        JsonDocument.Parse(handler.Bodies.Single()!).RootElement;

    [Fact]
    public async Task GetMineCallsTheOwnShowcaseRoute()
    {
        var (service, handler) = Create(HttpStatusCode.OK, MineBody);

        var result = await service.GetMineAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("66f1", result.Value!.ProviderRef);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v1/showcase/me", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SetTextAlwaysSendsBothFieldsIncludingNulls()
    {
        var (service, handler) = Create(HttpStatusCode.OK, MineBody);

        await service.SetTextAsync("Fine-line", null);

        Assert.Equal(HttpMethod.Put, handler.Requests.Single().Method);
        Assert.Equal("/api/v1/showcase/me/text", handler.Requests.Single().RequestUri!.AbsolutePath);
        var body = BodyOf(handler);
        Assert.Equal("Fine-line", body.GetProperty("tagline").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("about").ValueKind);
    }

    [Fact]
    public async Task RemovingThePhotoSendsAnExplicitNullHash()
    {
        var (service, handler) = Create(HttpStatusCode.OK, MineBody);

        await service.SetPhotoAsync(null);

        Assert.Equal("/api/v1/showcase/me/photo", handler.Requests.Single().RequestUri!.AbsolutePath);
        Assert.Equal(JsonValueKind.Null, BodyOf(handler).GetProperty("hash").ValueKind);
    }

    [Fact]
    public async Task SetLogoPutsTheHash()
    {
        var (service, handler) = Create(HttpStatusCode.OK, MineBody);

        await service.SetLogoAsync("abc");

        Assert.Equal(HttpMethod.Put, handler.Requests.Single().Method);
        Assert.Equal("/api/v1/showcase/me/logo", handler.Requests.Single().RequestUri!.AbsolutePath);
        Assert.Equal("abc", BodyOf(handler).GetProperty("hash").GetString());
    }

    [Fact]
    public async Task AddPortfolioItemPostsHashCaptionAndService()
    {
        var (service, handler) = Create(HttpStatusCode.Created,
            """{ "success": true, "data": { "hash": "abc", "caption": "Leaf", "width": 10, "height": 20 } }""");

        var result = await service.AddPortfolioItemAsync("abc", "Leaf", "svc");

        Assert.Equal(201, result.StatusCode);
        Assert.Equal("abc", result.Value!.Hash);
        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
        var body = BodyOf(handler);
        Assert.Equal("abc", body.GetProperty("hash").GetString());
        Assert.Equal("Leaf", body.GetProperty("caption").GetString());
        Assert.Equal("svc", body.GetProperty("serviceId").GetString());
    }

    [Fact]
    public async Task UpdatePortfolioItemPatchesOnTheHash()
    {
        var (service, handler) = Create(HttpStatusCode.OK, """{ "success": true, "data": { "hash": "abc" } }""");

        await service.UpdatePortfolioItemAsync("abc", null, null);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("/api/v1/showcase/me/portfolio/abc", request.RequestUri!.AbsolutePath);
        var body = BodyOf(handler);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("caption").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("serviceId").ValueKind);
    }

    [Fact]
    public async Task RemovingIsADeleteThatSucceedsOnNoContent()
    {
        var (service, handler) = Create(HttpStatusCode.NoContent);

        var result = await service.RemovePortfolioItemAsync("abc");

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Delete, handler.Requests.Single().Method);
        Assert.Equal("/api/v1/showcase/me/portfolio/abc", handler.Requests.Single().RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ReorderSendsTheWholePermutation()
    {
        var (service, handler) = Create(HttpStatusCode.OK, MineBody);

        await service.ReorderPortfolioAsync(["b", "a", "c"]);

        Assert.Equal("/api/v1/showcase/me/portfolio/order", handler.Requests.Single().RequestUri!.AbsolutePath);
        Assert.Equal(["b", "a", "c"],
            BodyOf(handler).GetProperty("hashes").EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    [Fact]
    public async Task PublicCodeIsAPostWithNoBody()
    {
        var (service, handler) = Create(HttpStatusCode.OK,
            """{ "success": true, "data": { "code": "K7Q2X9", "url": "https://x/api/v1/go/K7Q2X9" } }""");

        var result = await service.GetPublicCodeAsync();

        Assert.Equal("K7Q2X9", result.Value!.Code);
        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
        Assert.Null(handler.Bodies.Single());
    }

    [Theory]
    [InlineData(ShowcaseSource.Directory, "directory")]
    [InlineData(ShowcaseSource.Appointment, "appointment")]
    [InlineData(ShowcaseSource.Message, "message")]
    [InlineData(ShowcaseSource.Booking, "booking")]
    public async Task ViewingCarriesTheSource(ShowcaseSource source, string wire)
    {
        var (service, handler) = Create(HttpStatusCode.OK, """{ "success": true, "data": { "providerRef": "66f1" } }""");

        await service.GetShowcaseAsync("66f1", source);

        var uri = handler.Requests.Single().RequestUri!;
        Assert.Equal("/api/v1/showcase/66f1", uri.AbsolutePath);
        Assert.Equal($"?source={wire}", uri.Query);
    }

    [Theory]
    [InlineData(ShowcaseSource.Scan, "scan")]
    [InlineData(ShowcaseSource.Code, "code")]
    public async Task ByCodeCarriesTheSource(ShowcaseSource source, string wire)
    {
        var (service, handler) = Create(HttpStatusCode.OK, """{ "success": true, "data": { "providerRef": "66f1" } }""");

        await service.GetShowcaseByCodeAsync("K7Q2X9", source);

        var uri = handler.Requests.Single().RequestUri!;
        Assert.Equal("/api/v1/showcase/by-code/K7Q2X9", uri.AbsolutePath);
        Assert.Equal($"?source={wire}", uri.Query);
    }

    [Fact]
    public async Task LookupDeduplicatesAndCapsAtFiftyAddresses()
    {
        var (service, handler) = Create(HttpStatusCode.OK, """{ "success": true, "data": [] }""");
        var emails = Enumerable.Range(0, 70).Select(i => $"p{i}@x.dev").Append("P0@x.dev").ToList();

        await service.LookupAsync(emails);

        var sent = BodyOf(handler).GetProperty("emails").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(ShowcaseApiService.LookupMaxEmails, sent.Count);
        Assert.Equal(sent.Count, sent.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("/api/v1/showcase/lookup", handler.Requests.Single().RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task LookupWithNoAddressesMakesNoRequest()
    {
        var (service, handler) = Create(HttpStatusCode.OK, "[]");

        var result = await service.LookupAsync(["", " "]);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ReportSendsTheWireReason()
    {
        var (service, handler) = Create(HttpStatusCode.Accepted);

        var result = await service.ReportAsync("66f1", ShowcaseReportReason.NotTheirWork, "Copied", "abc");

        Assert.True(result.IsSuccess);
        Assert.Equal("/api/v1/showcase/66f1/report", handler.Requests.Single().RequestUri!.AbsolutePath);
        var body = BodyOf(handler);
        Assert.Equal("not_their_work", body.GetProperty("reason").GetString());
        Assert.Equal("Copied", body.GetProperty("detail").GetString());
        Assert.Equal("abc", body.GetProperty("portfolioHash").GetString());
    }

    [Fact]
    public async Task HideAndUnhideUsePutAndDeleteOnTheSameRoute()
    {
        var (service, handler) = Create(HttpStatusCode.NoContent);

        Assert.True((await service.HideAsync("66f1")).IsSuccess);
        Assert.True((await service.UnhideAsync("66f1")).IsSuccess);

        Assert.Equal([HttpMethod.Put, HttpMethod.Delete], handler.Requests.Select(r => r.Method).ToArray());
        Assert.All(handler.Requests, r => Assert.Equal("/api/v1/showcase/66f1/hide", r.RequestUri!.AbsolutePath));
    }

    [Fact]
    public async Task HiddenListIsRead()
    {
        var (service, _) = Create(HttpStatusCode.OK,
            """{ "success": true, "data": [{ "providerRef": "66f1", "firstName": "Mariana", "lastName": "Ruiz" }] }""");

        var result = await service.GetHiddenAsync();

        Assert.Equal("66f1", Assert.Single(result.Value!).ProviderRef);
    }

    [Fact]
    public async Task UploadSendsRawJpegBytesNotMultipart()
    {
        var (service, handler) = Create(HttpStatusCode.Created,
            """{ "success": true, "data": { "hash": "9f2c", "width": 1600, "height": 1067, "deduplicated": false } }""");
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

        var result = await service.UploadImageAsync(jpeg);

        Assert.Equal("9f2c", result.Value!.Hash);
        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/media", request.RequestUri!.AbsolutePath);
        Assert.Equal("image/jpeg", handler.ContentTypes.Single());
        Assert.Equal(jpeg, handler.RawBodies.Single());
    }

    [Fact]
    public async Task FetchReturnsTheImageBytes()
    {
        byte[] image = [1, 2, 3, 4];
        var handler = new RecordingHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(image)
        });
        var service = new ShowcaseApiService(Factory(handler));

        var result = await service.FetchImageAsync("66f1", "abc", MediaVariant.Thumb);

        Assert.Equal(image, result.Value);
        Assert.Equal("/api/v1/media/66f1/abc/thumb", handler.Requests.Single().RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData("unsupported-format", ShowcaseErrorCodes.UnsupportedFormat)]
    [InlineData("too-large", ShowcaseErrorCodes.TooLarge)]
    [InlineData("too-many-pixels", ShowcaseErrorCodes.TooManyPixels)]
    [InlineData("dimension-exceeded", ShowcaseErrorCodes.DimensionExceeded)]
    [InlineData("undecodable", ShowcaseErrorCodes.Undecodable)]
    public async Task UploadValidationCodesAreReadFromErrorsImage(string wire, string expected)
    {
        var (service, _) = Create(HttpStatusCode.BadRequest,
            $$"""{ "title": "One or more validation errors occurred.", "status": 400, "errors": { "image": ["{{wire}}"] } }""",
            "application/problem+json");

        var result = await service.UploadImageAsync([1]);

        Assert.Equal(expected, result.ErrorCode);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task AValidationEntryCarryingAMessageStillNamesItsCode()
    {
        var (service, _) = Create(HttpStatusCode.BadRequest,
            """{ "errors": { "image": ["too-many-pixels: The image is over 40 megapixels."] } }""");

        Assert.Equal(ShowcaseErrorCodes.TooManyPixels, (await service.UploadImageAsync([1])).ErrorCode);
    }

    [Fact]
    public async Task UnknownMediaIsReadFromErrorsHash()
    {
        var (service, _) = Create(HttpStatusCode.BadRequest, """{ "errors": { "hash": ["unknown-media"] } }""");

        Assert.Equal(ShowcaseErrorCodes.UnknownMedia, (await service.SetPhotoAsync("x")).ErrorCode);
    }

    [Fact]
    public async Task AnUncodedValidationFailureIsInvalid()
    {
        var (service, _) = Create(HttpStatusCode.BadRequest, """{ "errors": { "tagline": ["Too long."] } }""");

        Assert.Equal(ShowcaseErrorCodes.Invalid, (await service.SetTextAsync("x", null)).ErrorCode);
    }

    [Theory]
    [InlineData("portfolio-full", ShowcaseErrorCodes.PortfolioFull)]
    [InlineData("portfolio-changed", ShowcaseErrorCodes.PortfolioChanged)]
    public async Task ConflictsAreReadFromTheCodeExtension(string wire, string expected)
    {
        var (service, _) = Create(HttpStatusCode.Conflict,
            $$"""{ "title": "Conflict", "status": 409, "code": "{{wire}}" }""", "application/problem+json");

        var result = await service.AddPortfolioItemAsync("abc", null, null);

        Assert.Equal(expected, result.ErrorCode);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task NotFoundIsShowcaseNotFound()
    {
        var (service, _) = Create(HttpStatusCode.NotFound,
            """{ "type": "showcase-not-found", "title": "This provider isn't available", "status": 404 }""");

        Assert.Equal(ShowcaseErrorCodes.ShowcaseNotFound, (await service.GetShowcaseAsync("66f1", ShowcaseSource.Scan)).ErrorCode);
    }

    [Fact]
    public async Task AMediaNotFoundWithNoBodyIsStillShowcaseNotFound()
    {
        var (service, _) = Create(HttpStatusCode.NotFound);

        Assert.Equal(ShowcaseErrorCodes.ShowcaseNotFound, (await service.FetchImageAsync("a", "b", MediaVariant.Full)).ErrorCode);
    }

    [Theory]
    [InlineData("""{ "type": "storage-unavailable", "status": 503 }""")]
    [InlineData("")]
    public async Task ServiceUnavailableIsStorageUnavailable(string body)
    {
        var (service, _) = Create(HttpStatusCode.ServiceUnavailable, body);

        Assert.Equal(ShowcaseErrorCodes.StorageUnavailable, (await service.UploadImageAsync([1])).ErrorCode);
    }

    [Fact]
    public async Task TooManyRequestsIsRateLimited()
    {
        var (service, _) = Create(HttpStatusCode.TooManyRequests, "Too many requests", "text/plain");

        Assert.Equal(ShowcaseErrorCodes.RateLimited, (await service.GetShowcaseByCodeAsync("K7Q2X9", ShowcaseSource.Code)).ErrorCode);
    }

    [Fact]
    public async Task APayloadOverTheKestrelLimitIsTooLarge()
    {
        var (service, _) = Create(HttpStatusCode.RequestEntityTooLarge);

        Assert.Equal(ShowcaseErrorCodes.TooLarge, (await service.UploadImageAsync([1])).ErrorCode);
    }

    [Fact]
    public async Task AGatewayDestinationFailureNamesTheService()
    {
        var (service, _) = Create(HttpStatusCode.BadGateway,
            """{ "type": "gateway-destination-unreachable", "failedService": "provider" }""");

        var result = await service.GetMineAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("provider", result.FailedService);
    }

    [Fact]
    public async Task ATransportFailureIsNetwork()
    {
        var handler = new RecordingHandler(() => throw new HttpRequestException("down"));
        var service = new ShowcaseApiService(Factory(handler));

        Assert.Equal(ShowcaseErrorCodes.Network, (await service.GetMineAsync()).ErrorCode);
        Assert.Equal(ShowcaseErrorCodes.Network, (await service.HideAsync("x")).ErrorCode);
        Assert.Equal(ShowcaseErrorCodes.Network, (await service.FetchImageAsync("a", "b", MediaVariant.Thumb)).ErrorCode);
    }

    [Fact]
    public async Task ATimeoutIsNetwork()
    {
        var handler = new RecordingHandler(() => throw new TaskCanceledException("timeout"));
        var service = new ShowcaseApiService(Factory(handler));

        Assert.Equal(ShowcaseErrorCodes.Network, (await service.GetMineAsync()).ErrorCode);
    }

    [Fact]
    public void ClientCodesAreNeverReadOutOfAServerMessage() =>
        Assert.Equal(ShowcaseErrorCodes.Invalid,
            ShowcaseApiService.ClassifyFailure(HttpStatusCode.BadRequest,
                """{ "errors": { "about": ["unknown network invalid rate-limited"] } }"""));

    private sealed class RecordingHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> Bodies { get; } = [];
        public List<byte[]> RawBodies { get; } = [];
        public List<string?> ContentTypes { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is null)
            {
                Bodies.Add(null);
                ContentTypes.Add(null);
            }
            else
            {
                var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                RawBodies.Add(bytes);
                Bodies.Add(Encoding.UTF8.GetString(bytes));
                ContentTypes.Add(request.Content.Headers.ContentType?.MediaType);
            }

            return respond();
        }
    }
}
