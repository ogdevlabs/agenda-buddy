using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;

namespace AgendaBuddy.IntegrationTests.Persistence;

/// <summary>
/// F-018-T12 / AC-6. <c>PUT /api/v1/services/{email}</c> followed by <c>GET /api/v1/services/{email}</c>
/// proves the embedded <see cref="ServiceEntity"/> list's <c>[BsonElement]</c> mapping round-trips — this
/// is the entity <c>05-data-model.md</c> flags for breaking the snake_case convention
/// (<c>feeType</c>/<c>isActive</c> instead of <c>fee_type</c>/<c>is_active</c>): self-consistent through
/// this one entity's own (de)serialisation, so this test pins the round trip without re-litigating that
/// pre-existing naming defect.
/// </summary>
[Collection(HarnessCollection.Name)]
public class ServicesPersistenceTest(ServiceHostFixture<ServicesAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<ServicesAnchor>>
{
    private const string Email = "services-round-trip@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    [Fact]
    public async Task AC6_AServiceAddedToAProvider_ReadsBackWithEveryFieldIntact()
    {
        using var service = host.StartService("Production");

        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Services",
                LastName = "RoundTrip",
                Email = Email,
            });

        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"api/v1/services/{Email}")
        {
            Content = JsonContent.Create(new[]
            {
                new { Name = "Deep tissue massage", Description = "90 minutes", Fee = 120m, FeeType = FeeType.Fixed, IsActive = true },
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Email, TokenFactory.ProviderRole)),
            },
        };

        var putResponse = await service.Client.SendAsync(putRequest);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"api/v1/services/{Email}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _tokens.CreateToken(Email, TokenFactory.ProviderRole));
        var getResponse = await service.Client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // Parsed field-by-field rather than deserialised into ServiceEntity: Services does not register
        // ObjectIdJsonConverter (per ObjectIdJsonConverter's own remarks), so its "id" field is the
        // unusable {timestamp,machine,...} shape client-side deserialisation cannot parse. None of the
        // fields this test cares about are "id".
        using var body = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        var stored = Assert.Single(body.RootElement.EnumerateArray());

        Assert.Equal("Deep tissue massage", stored.GetProperty("name").GetString());
        Assert.Equal("90 minutes", stored.GetProperty("description").GetString());
        Assert.Equal(120m, stored.GetProperty("fee").GetDecimal());
        Assert.Equal((int)FeeType.Fixed, stored.GetProperty("feeType").GetInt32());
        Assert.True(stored.GetProperty("isActive").GetBoolean());
    }
}
