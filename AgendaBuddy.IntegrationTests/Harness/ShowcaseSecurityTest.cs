using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Extensions;
using AgendaBuddy.Library.Media;
using AgendaBuddy.Library.Showcase;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using SkiaSharp;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// The showcase's access boundary over real HTTP against a real MongoDB container: who can read an image, what a
/// takedown and a hide remove, what the anonymous <c>/go</c> route may and may not do, and what an erasure leaves.
/// </summary>
[Collection(HarnessCollection.Name)]
public class ShowcaseSecurityTest : IClassFixture<ServiceHostFixture<ProviderAnchor>>
{
    private const string ProviderEmail = "showcase-owner@example.com";
    private const string CustomerEmail = "showcase-viewer@example.com";
    private const string AppStoreUrl = "https://apps.apple.com/mx/app/agendame/id1";
    private const string IPhone =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148";
    private const string Desktop =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";

    private readonly ServiceHostFixture<ProviderAnchor> _host;
    private readonly TokenFactory _tokens;

    public ShowcaseSecurityTest(ServiceHostFixture<ProviderAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    private async Task<(ServiceHost Service, ObjectId ProviderId)> StartAsync(string? appStoreUrl = AppStoreUrl)
    {
        var settings = new Dictionary<string, string>
        {
            [ShowcaseExtensions.InMemoryMediaKey] = "true",
            ["Showcase:Go:BaseUrl"] = "https://gateway.example",
            ["Showcase:Go:AppStoreUrl"] = appStoreUrl ?? string.Empty,
        };
        var service = _host.StartService("Production", settings);

        var providerId = ObjectId.GenerateNewId();
        await service.Database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = providerId,
            FirstName = "Frida",
            LastName = "Kahlo",
            Email = ProviderEmail,
        });
        await service.Database.GetCollection<CustomerEntity>("customers").InsertOneAsync(new CustomerEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Diego",
            LastName = "Rivera",
            Email = CustomerEmail,
        });

        return (service, providerId);
    }

    private HttpRequestMessage Request(HttpMethod method, string route, string? subject, string role)
    {
        var request = new HttpRequestMessage(method, route);
        if (subject is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.CreateToken(subject, role));
        return request;
    }

    private HttpRequestMessage AsProvider(HttpMethod method, string route) =>
        Request(method, route, ProviderEmail, TokenFactory.ProviderRole);

    private HttpRequestMessage AsCustomer(HttpMethod method, string route) =>
        Request(method, route, CustomerEmail, TokenFactory.CustomerRole);

    private static byte[] Png(SKColor color)
    {
        using var bitmap = new SKBitmap(32, 24);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static async Task<JsonElement> DataOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private async Task<string> UploadAsync(ServiceHost service, SKColor color)
    {
        var request = AsProvider(HttpMethod.Post, "api/v1/media");
        request.Content = new ByteArrayContent(Png(color));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await DataOf(response)).GetProperty("hash").GetString()!;
    }

    private async Task AttachAsync(ServiceHost service, string hash)
    {
        var request = AsProvider(HttpMethod.Post, "api/v1/showcase/me/portfolio");
        request.Content = JsonContent.Create(new { hash, caption = "Retrato" });
        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── authentication and role ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("api/v1/showcase/me")]
    [InlineData("api/v1/showcase/000000000000000000000000")]
    [InlineData("api/v1/media/000000000000000000000000/abc/thumb")]
    public async Task EveryShowcaseReadNeedsASignedInCaller(string route)
    {
        var (service, _) = await StartAsync();
        using var lease = service;

        var response = await service.Client.SendAsync(Request(HttpMethod.Get, route, null, TokenFactory.CustomerRole));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ACustomerCannotWriteAShowcaseOrUploadMedia()
    {
        var (service, _) = await StartAsync();
        using var lease = service;

        var text = AsCustomer(HttpMethod.Put, "api/v1/showcase/me/text");
        text.Content = JsonContent.Create(new { tagline = "hola" });
        var upload = AsCustomer(HttpMethod.Post, "api/v1/media");
        upload.Content = new ByteArrayContent(Png(SKColors.Red));

        Assert.Equal(HttpStatusCode.Forbidden, (await service.Client.SendAsync(text)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await service.Client.SendAsync(upload)).StatusCode);
    }

    // ── media ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnUnattachedUploadIsVisibleOnlyToItsOwner()
    {
        var (service, providerId) = await StartAsync();
        using var lease = service;
        var hash = await UploadAsync(service, SKColors.Teal);
        var route = $"api/v1/media/{providerId}/{hash}/thumb";

        Assert.Equal(HttpStatusCode.NotFound, (await service.Client.SendAsync(AsCustomer(HttpMethod.Get, route))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await service.Client.SendAsync(AsProvider(HttpMethod.Get, route))).StatusCode);
    }

    [Fact]
    public async Task AnAttachedImageIsServedAsAReencodedJpegThatCannotBeSniffed()
    {
        var (service, providerId) = await StartAsync();
        using var lease = service;
        var hash = await UploadAsync(service, SKColors.Orange);
        await AttachAsync(service, hash);

        var response = await service.Client.SendAsync(AsCustomer(HttpMethod.Get, $"api/v1/media/{providerId}/{hash}/full"));
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, bytes[..3]);
    }

    [Fact]
    public async Task ABodyThatIsNotAnImageIsRefusedByName()
    {
        var (service, _) = await StartAsync();
        using var lease = service;

        var request = AsProvider(HttpMethod.Post, "api/v1/media");
        request.Content = new ByteArrayContent("<svg onload=alert(1)>"u8.ToArray());
        var response = await service.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ImageRejections.UnsupportedFormat, await response.Content.ReadAsStringAsync());
    }

    // ── takedown and hide ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ATakenDownShowcaseAndItsImagesAreGoneForEveryoneButTheOwner()
    {
        var (service, providerId) = await StartAsync();
        using var lease = service;
        var hash = await UploadAsync(service, SKColors.Purple);
        await AttachAsync(service, hash);

        await service.Database.GetCollection<ProviderShowcaseEntity>(ShowcaseCollections.Showcases).UpdateOneAsync(
            new BsonDocument("provider_id", providerId),
            new BsonDocument("$set", new BsonDocument("hidden_by_operator", true)));

        var showcase = await service.Client.SendAsync(AsCustomer(HttpMethod.Get, $"api/v1/showcase/{providerId}"));
        var media = await service.Client.SendAsync(AsCustomer(HttpMethod.Get, $"api/v1/media/{providerId}/{hash}/thumb"));
        var own = await service.Client.SendAsync(AsProvider(HttpMethod.Get, "api/v1/showcase/me"));

        Assert.Equal(HttpStatusCode.NotFound, showcase.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, media.StatusCode);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.True((await DataOf(own)).GetProperty("takenDown").GetBoolean());
    }

    [Fact]
    public async Task AHiddenProviderLeavesTheCustomersDirectoryAndShowcaseUntilUnhidden()
    {
        var (service, providerId) = await StartAsync();
        using var lease = service;

        var hide = await service.Client.SendAsync(AsCustomer(HttpMethod.Put, $"api/v1/showcase/{providerId}/hide"));
        var directory = await service.Client.SendAsync(AsCustomer(HttpMethod.Get, "api/v1/providers"));
        var showcase = await service.Client.SendAsync(AsCustomer(HttpMethod.Get, $"api/v1/showcase/{providerId}"));

        Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);
        Assert.DoesNotContain(ProviderEmail, await directory.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, showcase.StatusCode);

        await service.Client.SendAsync(AsCustomer(HttpMethod.Delete, $"api/v1/showcase/{providerId}/hide"));
        var restored = await service.Client.SendAsync(AsCustomer(HttpMethod.Get, "api/v1/providers"));

        Assert.Contains(ProviderEmail, await restored.Content.ReadAsStringAsync());
    }

    // ── the public code and /go ────────────────────────────────────────────────────────────────────

    private async Task<string> CreateCodeAsync(ServiceHost service)
    {
        var response = await service.Client.SendAsync(AsProvider(HttpMethod.Post, "api/v1/showcase/me/code"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await DataOf(response);
        var code = data.GetProperty("code").GetString()!;
        Assert.Equal($"https://gateway.example/api/v1/go/{code}", data.GetProperty("url").GetString());
        return code;
    }

    private static HttpRequestMessage Scan(string code, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/go/{code}");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        return request;
    }

    [Fact]
    public async Task AScanFromAnIphoneIsSentToTheAppStoreWithoutASignIn()
    {
        var (service, _) = await StartAsync();
        using var lease = service;
        var code = await CreateCodeAsync(service);

        // The TestServer handler on its own does not follow redirects; the default client would.
        using var noRedirects = new HttpClient(service.Server.CreateHandler()) { BaseAddress = service.Client.BaseAddress };
        var response = await noRedirects.SendAsync(Scan(code, IPhone));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(AppStoreUrl, response.Headers.Location?.ToString());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

        var counter = await service.Database.GetCollection<GoCounterEntity>(ShowcaseCollections.GoCounters)
            .Find(new BsonDocument("code", code)).FirstOrDefaultAsync();
        Assert.Equal(1, counter.Ios);
    }

    /// <summary>A known and an unknown code answer with the same page, so /go cannot be used to test codes.</summary>
    [Fact]
    public async Task AKnownAndAnUnknownCodeAreIndistinguishable()
    {
        var (service, _) = await StartAsync();
        using var lease = service;
        var code = await CreateCodeAsync(service);

        var known = await service.Client.SendAsync(Scan(code, Desktop));
        var unknown = await service.Client.SendAsync(Scan("ZZZZZZZZ", Desktop));

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.StartsWith("default-src 'none'", string.Join(";", known.Headers.GetValues("Content-Security-Policy")));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://apps.example/plaintext")]
    public async Task AStoreAddressThatIsNotHttpsIsNeverRedirectedToOrLinked(string storeUrl)
    {
        var (service, _) = await StartAsync(storeUrl);
        using var lease = service;
        var code = await CreateCodeAsync(service);

        var response = await service.Client.SendAsync(Scan(code, IPhone));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(storeUrl, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ASignedInCustomerOpensTheShowcaseByCodeAndTheScanIsRecordedAsItsSource()
    {
        var (service, providerId) = await StartAsync();
        using var lease = service;
        var code = await CreateCodeAsync(service);

        var response = await service.Client.SendAsync(AsCustomer(HttpMethod.Get, $"api/v1/showcase/by-code/{code}?source=scan"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(providerId.ToString(), (await DataOf(response)).GetProperty("providerRef").GetString());
        var visit = await service.Database.GetCollection<ShowcaseVisitEntity>(ShowcaseCollections.Visits)
            .Find(new BsonDocument("customer_email", CustomerEmail)).FirstOrDefaultAsync();
        Assert.Equal(ShowcaseSources.Scan, visit.FirstSource);
    }

    // ── reports ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AReportIsStoredAndAProviderCannotReportThemself()
    {
        var (service, providerId) = await StartAsync();
        using var lease = service;

        var report = AsCustomer(HttpMethod.Post, $"api/v1/showcase/{providerId}/report");
        report.Content = JsonContent.Create(new { reason = "spam" });
        var self = AsProvider(HttpMethod.Post, $"api/v1/showcase/{providerId}/report");
        self.Content = JsonContent.Create(new { reason = "spam" });
        var bogus = AsCustomer(HttpMethod.Post, $"api/v1/showcase/{providerId}/report");
        bogus.Content = JsonContent.Create(new { reason = "because" });

        Assert.Equal(HttpStatusCode.Accepted, (await service.Client.SendAsync(report)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await service.Client.SendAsync(self)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await service.Client.SendAsync(bogus)).StatusCode);

        var stored = await service.Database.GetCollection<ShowcaseReportEntity>(ShowcaseCollections.Reports)
            .Find(new BsonDocument("provider_id", providerId)).ToListAsync();
        Assert.Equal(CustomerEmail, Assert.Single(stored).ReporterEmail);
    }

    // ── erasure ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletingAProviderAccountRemovesTheShowcaseAndEveryStoredImage()
    {
        var (service, providerId) = await StartAsync();
        using var lease = service;
        var hash = await UploadAsync(service, SKColors.Gold);
        await AttachAsync(service, hash);
        await CreateCodeAsync(service);

        var response = await service.Client.SendAsync(AsProvider(HttpMethod.Delete, $"api/v1/providers/{ProviderEmail}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var byProvider = new BsonDocument("provider_id", providerId);
        Assert.Equal(0, await service.Database.GetCollection<BsonDocument>(ShowcaseCollections.Showcases).CountDocumentsAsync(byProvider));
        Assert.Equal(0, await service.Database.GetCollection<BsonDocument>(ShowcaseCollections.MediaRefs).CountDocumentsAsync(byProvider));
        Assert.Equal(0, await service.Database.GetCollection<BsonDocument>(ShowcaseCollections.GoCounters).CountDocumentsAsync(byProvider));
        var blobs = service.Services.GetRequiredService<IBlobStore>();
        Assert.Empty(await blobs.ListAsync(MediaKeys.Prefix(providerId)));
    }
}
