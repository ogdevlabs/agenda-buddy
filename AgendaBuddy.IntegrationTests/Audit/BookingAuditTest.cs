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
/// F-018-T13 / AC-7. <c>BookingAppointmentCommandHandler</c> writes a <c>BookAppointmentCommand</c> audit
/// event on both its success and failure branches (<c>BookingAppointmentCommandHandler.cs:20-40</c>).
/// CONSTITUTION §3 mandates this for every command result; before this task, nothing asserted it.
/// </summary>
/// <remarks>
/// Reads the <c>events</c> collection directly with <c>MongoDB.Driver</c>, never through
/// <see cref="IEventStore"/> — F-019/F-020 are expected to refactor that abstraction, and an assertion
/// routed through it could keep passing while the persisted document itself is wrong.
/// </remarks>
[Collection(HarnessCollection.Name)]
public class BookingAuditTest(ServiceHostFixture<BookingAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<BookingAnchor>>
{
    private const string ProviderEmail = "booking-audit-provider@example.com";
    private const string CustomerEmail = "booking-audit-customer@example.com";
    private const string MissingProviderEmail = "booking-audit-no-such-provider@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    private static async Task SeedProviderAsync(ServiceHost service) =>
        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Booking",
                LastName = "Audit",
                Email = ProviderEmail,
            });

    private static IMongoCollection<Event> Events(ServiceHost service) =>
        ConfiguredCollection.Of<Event>(service, "EventsCollection", "events");

    [Fact]
    public async Task AC7_ABookedAppointment_WritesASuccessAuditEvent()
    {
        using var service = host.StartService("Production");
        await SeedProviderAsync(service);

        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/booking/appointments")
        {
            Content = JsonContent.Create(new
            {
                EmailProvider = ProviderEmail,
                EmailCustomer = CustomerEmail,
                Start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc),
                DayOff = false,
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(ProviderEmail, TokenFactory.ProviderRole)),
            },
        };

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "BookAppointmentCommand"))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Status);
        Assert.Contains(ProviderEmail, audit.Data);
    }

    [Fact]
    public async Task AC7_ABookingAgainstAMissingProvider_WritesAFailedAuditEvent()
    {
        using var service = host.StartService("Production");
        // Deliberately NOT seeded: SearchAndUpdateProviderAppointments returns false when no provider
        // matches, which is the handler's only failure branch (BookingAppointmentCommandHandler.cs:57).

        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/booking/appointments")
        {
            Content = JsonContent.Create(new
            {
                EmailProvider = MissingProviderEmail,
                EmailCustomer = CustomerEmail,
                Start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc),
                DayOff = false,
            }),
            Headers =
            {
                // AssertOwnerAny accepts either participant email — the caller need not be a real
                // provider record for this failure branch to be reachable.
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(MissingProviderEmail, TokenFactory.ProviderRole)),
            },
        };

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var audit = await Events(service)
            .Find(Builders<Event>.Filter.Eq(e => e.Type, "BookAppointmentCommand"))
            .SingleOrDefaultAsync();

        Assert.NotNull(audit);
        Assert.Equal("Failed", audit.Status);
    }
}
