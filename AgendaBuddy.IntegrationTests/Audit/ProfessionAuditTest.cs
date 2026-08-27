using System.Net;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.IntegrationTests.Persistence;
using AgendaBuddy.EventAndCommands.Persistence;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Audit;

/// <summary>
/// F-018-T13 / AC-7. <c>GetProfessionByNameQueryHandler</c> is Profession's only reachable audited
/// handler: <c>POST /api/v1/professions</c> was deleted by F-016-T17 (<c>ProfessionWriteRouteRemovedTest</c>
/// pins that), so <c>AddProfessionCommandHandler</c> has no HTTP path at all. The GET query handler still
/// writes success/failure audit events, which is exactly why the task keeps Profession in Tier 3's scope.
/// </summary>
[Collection(HarnessCollection.Name)]
public class ProfessionAuditTest(ServiceHostFixture<ProfessionAnchor> host)
    : IClassFixture<ServiceHostFixture<ProfessionAnchor>>
{
    private static IMongoCollection<Event> Events(ServiceHost service) =>
        ConfiguredCollection.Of<Event>(service, "EventsCollection", "events");

    [Fact]
    public async Task AC7_ALookupOfASeededProfession_WritesASuccessAuditEvent()
    {
        using var service = host.StartService("Production");
        // The startup seed (ProfessionSeedHostedService) already populates "Coaching" — see
        // ProfessionPersistenceTest — so no extra seeding is needed here.

        var response = await service.Client.GetAsync("api/v1/professions/Coaching");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "GetProfessionByNameQuery"))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Status);
    }

    [Fact]
    public async Task AC7_ALookupOfAMissingProfession_WritesAFailedAuditEvent()
    {
        using var service = host.StartService("Production");

        var response = await service.Client.GetAsync("api/v1/professions/no-such-profession");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "GetProfessionByNameQuery"))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Failed", audit.Status);
    }
}
