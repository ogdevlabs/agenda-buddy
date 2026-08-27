using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Persistence;

/// <summary>
/// F-018-T12 / AC-6. <c>PUT /api/v1/providers/{email}</c> followed by <c>GET /api/v1/providers/{email}</c>
/// (as the owner, so the full — not the projected — shape comes back) proves <see cref="ProviderEntity"/>'s
/// <c>[BsonElement]</c> mapping round-trips, including its embedded <see cref="ServiceEntity"/> list.
/// </summary>
/// <remarks>
/// Not <c>POST /api/v1/providers</c>: <c>ProviderCreationGuardTest</c> already records that route as
/// Kafka-gated with no broker in this harness. <c>UpdateProviderCommandHandler</c> calls no Kafka client, so
/// a seeded starting document plus a real <c>PUT</c> is the write this suite can exercise end to end.
/// </remarks>
[Collection(HarnessCollection.Name)]
public class ProviderPersistenceTest(ServiceHostFixture<ProviderAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<ProviderAnchor>>
{
    private const string Email = "provider-round-trip@example.com";
    private const string SubscribedCustomer = "subscribed-customer@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    [Fact]
    public async Task AC6_AnUpdatedProvider_ReadsBackWithEveryFieldIntact()
    {
        using var service = host.StartService("Production");

        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Before",
                LastName = "Update",
                Email = Email,
            });

        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"api/v1/providers/{Email}")
        {
            Content = JsonContent.Create(new
            {
                FirstName = "After",
                LastName = "Update",
                Email,
                KafkaTopic = "provider-round-trip-topic",
                ServiceEntities = new[]
                {
                    new { Name = "60-min session", Description = "a real service", Fee = 65m },
                },
                SubscribedCustomerCollection = new[] { SubscribedCustomer },
                IsActive = true,
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Email, TokenFactory.ProviderRole)),
            },
        };

        var putResponse = await service.Client.SendAsync(putRequest);
        Assert.Equal(HttpStatusCode.Accepted, putResponse.StatusCode);

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"api/v1/providers/{Email}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _tokens.CreateToken(Email, TokenFactory.ProviderRole));
        var getResponse = await service.Client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // F-020-T11: the response is now wrapped in DataResponse<T> (ADR-049, following Booking's/
        // Calendar's/Profession's/Services' precedent) -- the object moved from the response root to a
        // "data" property. Parsed field-by-field rather than re-deserialised into ProviderEntity at
        // "data": ObjectIdJsonConverter IS registered for Provider, so a typed deserialise would also
        // work, but every sibling migration's persistence test parses field-by-field, and matching that
        // shape keeps this test's own diff minimal and consistent with the others.
        using var body = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        var read = body.RootElement.GetProperty("data");

        Assert.Equal("After", read.GetProperty("firstName").GetString());
        Assert.Equal("Update", read.GetProperty("lastName").GetString());
        Assert.Equal(Email, read.GetProperty("email").GetString());
        Assert.Equal("provider-round-trip-topic", read.GetProperty("kafkaTopic").GetString());
        Assert.Equal([SubscribedCustomer], read.GetProperty("subscribedCustomerCollection").EnumerateArray().Select(e => e.GetString()));
        Assert.True(read.GetProperty("isActive").GetBoolean());

        var serviceEntities = read.GetProperty("serviceEntities");
        var storedService = Assert.Single(serviceEntities.EnumerateArray());
        Assert.Equal("60-min session", storedService.GetProperty("name").GetString());
        Assert.Equal("a real service", storedService.GetProperty("description").GetString());
        Assert.Equal(65m, storedService.GetProperty("fee").GetDecimal());

        // Confirms the read went to the SAME collection the service itself is configured to use, not a
        // literal "providers" that happened to match by coincidence.
        var raw = await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .Find(Builders<ProviderEntity>.Filter.Eq(p => p.Email, Email))
            .SingleOrDefaultAsync();
        Assert.NotNull(raw);
        Assert.Equal("After", raw.FirstName);
    }
}
