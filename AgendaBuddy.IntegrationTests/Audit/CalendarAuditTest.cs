using System.Net;
using System.Text.Json;
using System.Net.Http.Headers;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.IntegrationTests.Persistence;
using AgendaBuddy.EventAndCommands.Persistence;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Audit;

/// <summary>
/// AC-7. Calendar is read-only, but its QUERY handlers audit too — on both success and
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

    // Contract CHANGED 2026-08-29, and this test changed with it rather than being deleted.
    //
    // It used to assert 404 + a "Failed" audit for an address matching no provider, because looking the
    // address up as a provider was the handler's only path. That is exactly what made a CUSTOMER's own
    // appointments unreachable — a customer never has a ProviderEntity, so every customer got 404 for
    // their own calendar. The handler now falls through to gathering the caller's appointments from the
    // provider side, so "no provider with this email" is no longer a failure: it is a successful read that
    // happens to be empty, which is also what a customer with no bookings legitimately gets.
    //
    // The route is ownership-guarded, so the caller already owns the address; there is nothing useful to
    // distinguish "unknown address" from "no appointments yet", and 404 for the latter was the bug. What
    // still matters — and is what this now pins — is that the read is ATTRIBUTED: an audit event is written
    // either way, so a query cannot happen unrecorded.
    [Fact]
    public async Task AC7_ACheckOfAnAddressWithNoAppointments_Is200AndStillWritesAnAuditEvent()
    {
        using var service = host.StartService("Production");
        // Deliberately not seeded: no provider and no appointments anywhere for this address.

        var response = await service.Client.SendAsync(
            Read($"api/v1/calendar/appointments/{MissingProviderEmail}", MissingProviderEmail));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(body.RootElement.GetProperty("data").EnumerateArray());

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "CheckCalendarAppointmentsQuery"))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Status);
    }
}
