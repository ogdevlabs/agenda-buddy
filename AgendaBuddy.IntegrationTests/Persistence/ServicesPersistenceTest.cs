using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;

namespace AgendaBuddy.IntegrationTests.Persistence;

/// <summary>
/// <c>PUT /api/v1/services/{email}</c> followed by <c>GET /api/v1/services/{email}</c>
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
                // A service must be classified under one of the provider's own professions
                // (AddServicesToProviderCommandHandler), so the provider has to hold it first.
                Professions = ["Massage Therapy"],
            });

        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"api/v1/services/{Email}")
        {
            Content = JsonContent.Create(new[]
            {
                new
                {
                    Name = "Deep tissue massage",
                    Description = "90 minutes",
                    Fee = 120m,
                    FeeType = FeeType.Fixed,
                    IsActive = true,
                    ProfessionName = "Massage Therapy",
                },
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
        //
        // The response is wrapped in DataResponse<T> (ADR-049) -- the array is under a
        // "data" property, not the response root.
        using var body = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        var stored = Assert.Single(body.RootElement.GetProperty("data").EnumerateArray());

        Assert.Equal("Deep tissue massage", stored.GetProperty("name").GetString());
        Assert.Equal("90 minutes", stored.GetProperty("description").GetString());
        Assert.Equal(120m, stored.GetProperty("fee").GetDecimal());
        Assert.Equal((int)FeeType.Fixed, stored.GetProperty("feeType").GetInt32());
        Assert.True(stored.GetProperty("isActive").GetBoolean());

        // The classification round-trips too — it is what scopes the service to a profession in the
        // customer-facing booking flow, so losing it silently would make the service unbookable.
        Assert.Equal("Massage Therapy", stored.GetProperty("professionName").GetString());
    }

    // The rule the test above had to be updated for: a service must name one of the provider's own
    // professions. Checked before any write, so a rejected add leaves nothing behind — the earlier shape
    // of this handler persisted the appointment/service first and validated afterwards.
    [Theory]
    [InlineData(null)]                    // unclassified
    [InlineData("Something Else")]        // a profession this provider does not hold
    public async Task AServiceThatNamesNoProfessionOfThisProviderIsRejected_AndNothingIsStored(string? professionName)
    {
        using var service = host.StartService("Production");
        var email = $"reject-{Guid.NewGuid():N}@example.com";

        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Services",
                LastName = "Reject",
                Email = email,
                Professions = ["Massage Therapy"],
            });

        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"api/v1/services/{email}")
        {
            Content = JsonContent.Create(new[]
            {
                new { Name = "Unclassified", Description = "no profession", Fee = 10m, FeeType = FeeType.Fixed, IsActive = true, ProfessionName = professionName },
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(email, TokenFactory.ProviderRole)),
            },
        };

        Assert.NotEqual(HttpStatusCode.OK, (await service.Client.SendAsync(putRequest)).StatusCode);

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"api/v1/services/{email}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _tokens.CreateToken(email, TokenFactory.ProviderRole));

        using var body = JsonDocument.Parse(
            await (await service.Client.SendAsync(getRequest)).Content.ReadAsStringAsync());

        Assert.Empty(body.RootElement.GetProperty("data").EnumerateArray());
    }
}
