using System.Net;
using System.Net.Http.Headers;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.IntegrationTests.Persistence;
using EventAndCommands.Persistence;
using Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Audit;

/// <summary>
/// F-018-T13 / AC-7. Calendar is read-only, but its QUERY handlers audit too — on both success and
/// failure paths (<c>CheckCalendarAppointmentsQueryHandler.cs</c>), which is exactly why the task keeps
/// Calendar in Tier 3's scope despite it having no command handler at all.
/// </summary>
/// <remarks>
/// Reads the <c>events</c> collection directly with <c>MongoDB.Driver</c>, not through
/// <see cref="IEventStore"/> — see <see cref="BookingAuditTest"/>'s remarks for why.
/// </remarks>
[Collection(HarnessCollection.Name)]
public class CalendarAuditTest(ServiceHostFixture<CalendarAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<CalendarAnchor>>
{
    private const string ProviderEmail = "calendar-audit-provider@example.com";
    private const string MissingProviderEmail = "calendar-audit-no-such-provider@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    private HttpRequestMessage Read(string route, string subject) =>
        new(HttpMethod.Get, route)
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(subject, TokenFactory.ProviderRole)),
            },
        };

    private static IMongoCollection<Event> Events(ServiceHost service) =>
        ConfiguredCollection.Of<Event>(service, "EventsCollection", "events");

    [Fact]
    public async Task AC7_ACheckOfASeededProvidersAppointments_WritesASuccessAuditEvent()
    {
        using var service = host.StartService("Production");

        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Calendar",
                LastName = "Audit",
                Email = ProviderEmail,
            });

        var response = await service.Client.SendAsync(
            Read($"api/v1/calendar/appointments/{ProviderEmail}", ProviderEmail));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "CheckCalendarAppointmentsQuery"))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Status);
    }

    [Fact]
    public async Task AC7_ACheckOfAMissingProvidersAppointments_WritesAFailedAuditEvent()
    {
        using var service = host.StartService("Production");
        // Deliberately not seeded: providerService.FindProvidersAsync returns null, which is the
        // handler's only failure branch (CheckCalendarAppointmentsQueryHandler.cs).

        var response = await service.Client.SendAsync(
            Read($"api/v1/calendar/appointments/{MissingProviderEmail}", MissingProviderEmail));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // the route maps the handler's null! to 404

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "CheckCalendarAppointmentsQuery"))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Failed", audit.Status);
    }
}
