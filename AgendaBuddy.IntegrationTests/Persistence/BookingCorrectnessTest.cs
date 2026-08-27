using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;

namespace AgendaBuddy.IntegrationTests.Persistence;

/// <summary>
/// F-025: <c>POST /api/v1/booking/appointments</c> used to accept any Start/End pair, including
/// backwards, past-dated, and overlapping appointments for the same provider. These pin the three
/// invariants added to close that gap.
/// </summary>
[Collection(HarnessCollection.Name)]
public class BookingCorrectnessTest(ServiceHostFixture<BookingAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<BookingAnchor>>
{
    private const string ProviderEmail = "booking-correctness-provider@example.com";
    private const string CustomerEmail = "booking-correctness-customer@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    private static async Task SeedProviderAsync(ServiceHost service) =>
        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Correctness",
                LastName = "Provider",
                Email = ProviderEmail,
            });

    private HttpRequestMessage BookRequest(DateTime start, DateTime end) =>
        new(HttpMethod.Post, "api/v1/booking/appointments")
        {
            Content = JsonContent.Create(new
            {
                EmailProvider = ProviderEmail,
                EmailCustomer = CustomerEmail,
                Start = start,
                End = end,
                DayOff = false,
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(ProviderEmail, TokenFactory.ProviderRole)),
            },
        };

    [Fact]
    public async Task PostAppointments_EndBeforeStart_Returns400()
    {
        using var service = host.StartService("Production");
        await SeedProviderAsync(service);

        var response = await service.Client.SendAsync(
            BookRequest(new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAppointments_StartInThePast_Returns400()
    {
        using var service = host.StartService("Production");
        await SeedProviderAsync(service);

        var response = await service.Client.SendAsync(
            BookRequest(new DateTime(2020, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2020, 1, 1, 11, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAppointments_OverlapsAnExistingAppointmentForTheSameProvider_Returns400()
    {
        using var service = host.StartService("Production");
        await SeedProviderAsync(service);

        var start = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 9, 2, 11, 0, 0, DateTimeKind.Utc);

        var first = await service.Client.SendAsync(BookRequest(start, end));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Overlaps the first appointment's second half.
        var second = await service.Client.SendAsync(
            BookRequest(start.AddMinutes(30), end.AddMinutes(30)));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task PostAppointments_ImmediatelyAdjacentToAnExistingAppointment_Returns201()
    {
        using var service = host.StartService("Production");
        await SeedProviderAsync(service);

        var start = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 9, 3, 11, 0, 0, DateTimeKind.Utc);

        var first = await service.Client.SendAsync(BookRequest(start, end));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Starts exactly when the first one ends -- not an overlap.
        var second = await service.Client.SendAsync(BookRequest(end, end.AddHours(1)));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }
}
