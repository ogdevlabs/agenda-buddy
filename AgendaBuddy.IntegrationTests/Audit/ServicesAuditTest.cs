using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.IntegrationTests.Persistence;
using EventAndCommands.Persistence;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Audit;

/// <summary>
/// F-018-T13 / AC-7. <c>AddServicesToProviderCommandHandler</c> writes an audit event on both its
/// success and failure branches (<c>AddServicesToProviderCommandHandler.cs</c>) — unlike its sibling
/// <c>UpdateServicesFromProviderCommandHandler</c>, whose provider-not-found branch returns <c>null!</c>
/// with no audit write at all (a real, pre-existing gap, out of this task's scope to fix). Using the
/// handler that actually audits both branches is what makes this a genuine AC-7 pin rather than a test
/// that would need the handler fixed first.
/// </summary>
[Collection(HarnessCollection.Name)]
public class ServicesAuditTest(ServiceHostFixture<ServicesAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<ServicesAnchor>>
{
    private const string Email = "services-audit@example.com";
    private const string MissingEmail = "services-audit-no-such-provider@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    private static IMongoCollection<Event> Events(ServiceHost service) =>
        ConfiguredCollection.Of<Event>(service, "EventsCollection", "events");

    [Fact]
    public async Task AC7_AServiceAddedToAnExistingProvider_WritesASuccessAuditEvent()
    {
        using var service = host.StartService("Production");

        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Services",
                LastName = "Audit",
                Email = Email,
            });

        var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/services/{Email}")
        {
            Content = JsonContent.Create(new[]
            {
                new { Name = "Deep tissue massage", Description = "90 minutes", Fee = 120m },
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Email, TokenFactory.ProviderRole)),
            },
        };

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "AddServicesToProviderCommand"))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Status);
    }

    [Fact]
    public async Task AC7_AServiceAddedToAMissingProvider_WritesAFailedAuditEvent()
    {
        using var service = host.StartService("Production");
        // Deliberately not seeded: AddServicesToProviderCommandHandler's failure branch fires when
        // providerService.FindProvidersAsync finds no match.

        var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/services/{MissingEmail}")
        {
            Content = JsonContent.Create(new[]
            {
                new { Name = "Deep tissue massage", Description = "90 minutes", Fee = 120m },
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(MissingEmail, TokenFactory.ProviderRole)),
            },
        };

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "AddServicesToProviderCommand"))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Failed", audit.Status);
    }
}
