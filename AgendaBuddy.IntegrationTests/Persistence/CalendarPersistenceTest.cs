using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;

namespace AgendaBuddy.IntegrationTests.Persistence;

/// <summary>
/// Calendar is read-only (two GETs, <c>Calendar/Program.cs:113,141</c>), so this test
/// SEEDS a <see cref="ProviderEntity"/> directly into the collection the service itself
/// resolves (<see cref="ConfiguredCollection"/>) and then reads it back through both real routes.
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
        //
        // The response is wrapped in DataResponse<T> (ADR-049) -- the array is under a
        // "data" property, not the response root.
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var stored = Assert.Single(body.RootElement.GetProperty("data").EnumerateArray());

        Assert.Equal("calendar-persistence-appt-1", stored.GetProperty("identifier").GetString());
        Assert.Equal(Owner, stored.GetProperty("emailProvider").GetString());
        Assert.Equal(CustomerInTheBook, stored.GetProperty("emailCustomer").GetString());
        Assert.Equal(start, stored.GetProperty("start").GetDateTime());
        Assert.Equal(end, stored.GetProperty("end").GetDateTime());
        Assert.False(stored.GetProperty("dayOff").GetBoolean());
        Assert.Equal((int)AppointmentStatus.Booked, stored.GetProperty("appointmentStatus").GetInt32());
    }

    [Fact]
    public async Task AppointmentPage_ReturnsOnlyTheRequestedFiveItems()
    {
        const string owner = "calendar-page-owner@example.com";
        using var service = host.StartService("Production");
        var appointments = Enumerable.Range(1, 7)
            .Select(day => new AppointmentEntity
            {
                Identifier = $"calendar-page-{day}",
                EmailProvider = owner,
                EmailCustomer = CustomerInTheBook,
                Start = new DateTime(2026, 9, day, 10, 0, 0, DateTimeKind.Utc),
                End = new DateTime(2026, 9, day, 11, 0, 0, DateTimeKind.Utc),
                AppointmentStatus = AppointmentStatus.Completed
            })
            .ToList();

        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Paged",
                LastName = "Provider",
                Email = owner,
                AppointmentEntities = appointments
            });

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"api/v1/calendar/appointments/{owner}/page?segment=done&page=2&pageSize=5");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _tokens.CreateToken(owner, TokenFactory.ProviderRole));

        var response = await service.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var page = body.RootElement.GetProperty("data");
        Assert.Equal(7, page.GetProperty("totalCount").GetInt64());
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.Equal(5, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, page.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ProviderHistory_PagesMoreThanTwentyAppointmentsWithoutDuplicatesOrGaps()
    {
        const string owner = "calendar-large-history-provider@example.com";
        const int totalAppointments = 23;
        const int pageSize = 5;
        using var service = host.StartService("Production");
        var today = DateTime.UtcNow.Date;
        var appointments = Enumerable.Range(1, totalAppointments)
            .Select(daysAgo => new AppointmentEntity
            {
                Identifier = $"calendar-history-{daysAgo:D2}",
                EmailProvider = owner,
                EmailCustomer = $"customer-{daysAgo:D2}@example.com",
                Start = today.AddDays(-daysAgo).AddHours(10),
                End = today.AddDays(-daysAgo).AddHours(11),
                AppointmentStatus = AppointmentStatus.Completed
            })
            .ToList();

        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Large",
                LastName = "History",
                Email = owner,
                AppointmentEntities = appointments
            });

        var identifiers = new List<string>();
        for (var pageNumber = 1; pageNumber <= 5; pageNumber++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"api/v1/calendar/appointments/{owner}/page?segment=done&page={pageNumber}&pageSize={pageSize}");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", _tokens.CreateToken(owner, TokenFactory.ProviderRole));

            var response = await service.Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var page = body.RootElement.GetProperty("data");
            Assert.Equal(totalAppointments, page.GetProperty("totalCount").GetInt64());
            Assert.Equal(pageNumber, page.GetProperty("page").GetInt32());
            Assert.Equal(pageSize, page.GetProperty("pageSize").GetInt32());

            var items = page.GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(pageNumber < 5 ? pageSize : 3, items.Count);
            identifiers.AddRange(items.Select(item => item.GetProperty("identifier").GetString()!));
        }

        Assert.Equal(totalAppointments, identifiers.Count);
        Assert.Equal(totalAppointments, identifiers.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            Enumerable.Range(1, totalAppointments).Select(daysAgo => $"calendar-history-{daysAgo:D2}"),
            identifiers);
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

        // Wrapped in DataResponse<T> -- see the remarks above the appointments assertion.
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var slots = body.RootElement.GetProperty("data").EnumerateArray().ToList();
        Assert.NotEmpty(slots);
    }
}
