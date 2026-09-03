using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.IntegrationTests.Persistence;
using AgendaBuddy.EventAndCommands.Persistence;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Audit;

/// <summary>
/// AC-7. <c>UpdateProviderCommandHandler</c> and <c>AddProviderCommandHandler</c> both write an
/// audit event through <see cref="IEventStore"/>.
/// </summary>
/// <remarks>
/// Both are asserted on their success path. <c>POST</c> used to be reachable only as a FAILURE here,
/// because creating a provider called a message broker that this harness never started; creating one
/// now touches no broker, so the success path is the one that runs.
/// </remarks>
[Collection(HarnessCollection.Name)]
public class ProviderAuditTest(ServiceHostFixture<ProviderAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<ProviderAnchor>>
{
    private const string Email = "provider-audit@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    private static IMongoCollection<Event> Events(ServiceHost service) =>
        ConfiguredCollection.Of<Event>(service, "EventsCollection", "events");

    [Fact]
    public async Task AC7_AnUpdatedProvider_WritesASuccessAuditEvent()
    {
        using var service = host.StartService("Production");

        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Before",
                LastName = "Audit",
                Email = Email,
            });

        var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/providers/{Email}")
        {
            Content = JsonContent.Create(new { FirstName = "After", LastName = "Audit", Email }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Email, TokenFactory.ProviderRole)),
            },
        };

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "UpdateProviderCommand"))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Status);
    }

    [Fact]
    public async Task AC7_ACreate_WritesASuccessAuditEvent()
    {
        using var service = host.StartService("Production");

        var newProviderEmail = $"provider-audit-create-{Guid.NewGuid():N}@example.com";

        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/providers")
        {
            Content = JsonContent.Create(new
            {
                FirstName = "Audit",
                LastName = $"Create-{Guid.NewGuid():N}",
                Email = newProviderEmail,
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(newProviderEmail, TokenFactory.ProviderRole)),
            },
        };

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Regex(e => e.Type, new BsonRegularExpression("^AddProviderCommand")))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Status);
        Assert.Equal("AddProviderCommand", audit.Type);
    }
}
