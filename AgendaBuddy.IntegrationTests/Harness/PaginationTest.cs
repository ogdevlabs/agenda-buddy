using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// F-016 AC-15: both list endpoints accept pagination, return a bounded page with total-count metadata, and
/// cap page size server-side even when a larger one is requested.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the contract F-015 is written against</b> (ADR-023, <c>api-contracts.md</c> §4), which is why
/// the envelope's exact property set is asserted rather than just its contents.
/// </para>
/// <para>
/// <b>The cap is a security control.</b> An uncapped <c>pageSize</c> restores the full-dataset dump the
/// feature exists to remove — so the interesting case is <c>pageSize=100000</c>, not <c>pageSize=10</c>.
/// </para>
/// <para>
/// Paging happens at the <b>database</b>, not after the fact: the query handler calls
/// <c>GetPagedAsync(skip, take)</c>. Slicing a fully-read list in the endpoint would bound the response while
/// leaving the extraction unbounded, which is the opposite of the point. That is not directly observable over
/// HTTP, so it is pinned by <c>MongoDbRepositoryTest</c> and the handler wiring rather than claimed here.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class PaginationTest : IClassFixture<ServiceHostFixture<ProviderAnchor>>
{
    private const int SeededProviders = 30;
    private static readonly string[] EnvelopeProperties = ["items", "page", "pageSize", "totalCount"];

    private readonly ServiceHostFixture<ProviderAnchor> _host;
    private readonly TokenFactory _tokens;

    public PaginationTest(ServiceHostFixture<ProviderAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    private async Task<ServiceHost> StartWithProviders()
    {
        var service = _host.StartService("Production");

        var providers = Enumerable.Range(0, SeededProviders).Select(i => new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = $"Provider{i:D3}",
            LastName = "Seeded",
            Email = $"provider{i:D3}@example.com",
        }).ToList();

        await service.Database.GetCollection<ProviderEntity>("providers").InsertManyAsync(providers);
        return service;
    }

    private async Task<JsonDocument> Read(ServiceHost service, string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/providers{query}");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _tokens.CreateToken("reader@example.com", TokenFactory.CustomerRole));

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static int ItemCount(JsonDocument page) => page.RootElement.GetProperty("items").GetArrayLength();

    [Fact]
    public async Task AC15_TheEnvelopeHasExactlyTheDocumentedShape()
    {
        using var service = await StartWithProviders();
        using var page = await Read(service, string.Empty);

        Assert.Equal(
            EnvelopeProperties,
            page.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        // Defaults from ADR-023.
        Assert.Equal(1, page.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(25, page.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(SeededProviders, page.RootElement.GetProperty("totalCount").GetInt64());
        Assert.Equal(25, ItemCount(page));
    }

    [Fact]
    public async Task AC15_TotalCountIsTheWholeCollection_NotThePage()
    {
        using var service = await StartWithProviders();
        using var page = await Read(service, "?page=1&pageSize=10");

        Assert.Equal(10, ItemCount(page));
        Assert.Equal(SeededProviders, page.RootElement.GetProperty("totalCount").GetInt64());
    }

    [Fact]
    public async Task AC15_ASecondPageReturnsDifferentRecords()
    {
        // Without this, a paging bug that ignored `page` would satisfy every size assertion above.
        using var service = await StartWithProviders();
        using var first = await Read(service, "?page=1&pageSize=10");
        using var second = await Read(service, "?page=2&pageSize=10");

        var firstEmails = first.RootElement.GetProperty("items").EnumerateArray()
            .Select(p => p.GetProperty("email").GetString()).ToList();
        var secondEmails = second.RootElement.GetProperty("items").EnumerateArray()
            .Select(p => p.GetProperty("email").GetString()).ToList();

        Assert.Equal(10, secondEmails.Count);
        Assert.Empty(firstEmails.Intersect(secondEmails));
    }

    [Fact]
    public async Task AC15_AnOversizedPageSizeIsClampedAndTheEffectiveValueIsEchoed()
    {
        // The security-relevant case. Clamped rather than rejected (ADR-023), and the response reports the
        // size actually applied -- which is what lets an honest client discover the cap.
        using var service = await StartWithProviders();
        using var page = await Read(service, "?pageSize=100000");

        Assert.Equal(100, page.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(SeededProviders, ItemCount(page));  // fewer than the cap exist
        Assert.Equal(SeededProviders, page.RootElement.GetProperty("totalCount").GetInt64());
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-5")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=-1")]
    public async Task AC15_OutOfRangeValuesAreClampedRatherThanRejected(string query)
    {
        // A 400 would tell an attacker the exact boundary and leave an honest client no way to discover the
        // cap. Read() already asserts 200.
        using var service = await StartWithProviders();
        using var page = await Read(service, query);

        Assert.True(page.RootElement.GetProperty("page").GetInt32() >= 1);
        Assert.True(page.RootElement.GetProperty("pageSize").GetInt32() >= 1);
    }

    [Fact]
    public async Task AC15_APagePastTheEndIs200WithAnEmptyArray_NotA404()
    {
        // ADR-023 retires the 204 these endpoints used to return: a client always gets a parseable body.
        using var service = await StartWithProviders();
        using var page = await Read(service, "?page=500&pageSize=25");

        Assert.Equal(0, ItemCount(page));
        Assert.Equal(SeededProviders, page.RootElement.GetProperty("totalCount").GetInt64());
    }
}
