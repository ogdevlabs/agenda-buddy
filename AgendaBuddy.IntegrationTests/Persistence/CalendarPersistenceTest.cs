using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgendaBuddy.IntegrationTests.Harness;
using Library.Entities;
using MongoDB.Bson;

namespace AgendaBuddy.IntegrationTests.Persistence;

/// <summary>
/// F-018-T12 / AC-6. Calendar is read-only (two GETs, <c>Calendar/Program.cs:113,141</c>), so tier 2 is
/// satisfied by SEEDING a <see cref="ProviderEntity"/> directly into the collection the service itself
/// resolves (<see cref="ConfiguredCollection"/>) and then reading it back through both real routes —
/// exactly the shape the task prescribes.
/// </summary>
/// <remarks>
/// <c>CheckCalendarAppointmentsQueryHandler</c> returns <c>providerEntity.AppointmentEntities</c> —
/// the EMBEDDED list, not the standalone <c>appointments</c> collection — so seeding the provider with an
/// embedded appointment is what this route actually reads, and is what proves the nested
/// <see cref="AppointmentEntity"/> mapping round-trips through the provider document.
/// </remarks>
[Collection(HarnessCollection.Name)]
public class CalendarPersistenceTest(ServiceHostFixture<CalendarAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<CalendarAnchor>>
{
    private const string Owner = "calendar-persistence-owner@example.com";
    private const string CustomerInTheBook = "calendar-persistence-customer@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    private HttpRequestMessage Read(string route) =>
        new(HttpMethod.Get, route)
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Owner, TokenFactory.ProviderRole)),
            },
        };

    [Fact]
    public async Task AC6_ASeededAppointment_ReadsBackFromCheckCalendarAppointments()
    {
        using var service = host.StartService("Production");

        var start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc);

        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Calendar",
                LastName = "Persistence",
                Email = Owner,
                AppointmentEntities =
                [
                    new AppointmentEntity
                    {
                        Identifier = "calendar-persistence-appt-1",
                        EmailProvider = Owner,
                        EmailCustomer = CustomerInTheBook,
                        Start = start,
                        End = end,
                        DayOff = false,
                        AppointmentStatus = AppointmentStatus.Booked,
                    },
                ],
            });

        var response = await service.Client.SendAsync(Read($"api/v1/calendar/appointments/{Owner}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Parsed field-by-field rather than deserialised into AppointmentEntity: Calendar does not
        // register ObjectIdJsonConverter (unlike Booking/Customer/Provider, per ObjectIdJsonConverter's
        // own remarks), so its "id" field is the unusable {timestamp,machine,...} shape client-side
        // deserialisation cannot parse. None of the fields this test cares about are "id".
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var stored = Assert.Single(body.RootElement.EnumerateArray());

        Assert.Equal("calendar-persistence-appt-1", stored.GetProperty("identifier").GetString());
        Assert.Equal(Owner, stored.GetProperty("emailProvider").GetString());
        Assert.Equal(CustomerInTheBook, stored.GetProperty("emailCustomer").GetString());
        Assert.Equal(start, stored.GetProperty("start").GetDateTime());
        Assert.Equal(end, stored.GetProperty("end").GetDateTime());
        Assert.False(stored.GetProperty("dayOff").GetBoolean());
        Assert.Equal((int)AppointmentStatus.Booked, stored.GetProperty("appointmentStatus").GetInt32());
    }

    [Fact]
    public async Task AC6_TheSeededProvider_IsFoundByCheckCalendarAvailability()
    {
        // Availability is a computed 30-day slot grid (SupportTools.GetThirtyDaysCalendarAvailability) —
        // there is no other field of the seeded document for this route to echo back. What this route CAN
        // prove is the round trip of ProviderEntity's own "email" field: found (200, slots) only if the
        // filter used to look the provider up matches what was actually stored.
        using var service = host.StartService("Production");

        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Calendar",
                LastName = "Availability",
                Email = Owner,
            });

        var response = await service.Client.SendAsync(Read($"api/v1/calendar/availability/{Owner}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var slots = await response.Content.ReadFromJsonAsync<List<DateTime>>();
        Assert.NotNull(slots);
        Assert.NotEmpty(slots);
    }
}
