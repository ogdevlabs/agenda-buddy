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
/// <c>POST /api/v1/booking/appointments</c> writes a real <see cref="AppointmentEntity"/>
/// through the entire pipeline, and reading it back from the SAME <c>appointments</c> collection the
/// service itself resolves (<see cref="ConfiguredCollection"/>) proves every <c>[BsonElement]</c> on the
/// entity round-trips — not just the ones the response DTO happens to echo.
/// </summary>
/// <remarks>
/// A pre-existing provider is a real precondition here, not test scaffolding:
/// <c>BookingAppointmentCommandHandler.SearchAndUpdateProviderAppointments</c> looks the provider up by
/// email before it will insert anything, so the appointment write never happens without one.
/// </remarks>
[Collection(HarnessCollection.Name)]
public class BookingPersistenceTest(ServiceHostFixture<BookingAnchor> host, CryptoSessionFixture crypto)
    : IClassFixture<ServiceHostFixture<BookingAnchor>>
{
    private const string ProviderEmail = "booking-round-trip-provider@example.com";
    private const string CustomerEmail = "booking-round-trip-customer@example.com";

    private readonly TokenFactory _tokens = new(crypto);

    private static async Task SeedProviderAsync(ServiceHost service) =>
        await ConfiguredCollection.Of<ProviderEntity>(service, "ProvidersCollection", "providers")
            .InsertOneAsync(new ProviderEntity
            {
                Id = ObjectId.GenerateNewId(),
                FirstName = "Round",
                LastName = "Trip",
                Email = ProviderEmail,
            });

    [Fact]
    public async Task AC6_ABookedAppointment_ReadsBackWithEveryFieldIntact()
    {
        using var service = host.StartService("Production");
        await SeedProviderAsync(service);

        var start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc);

        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/booking/appointments")
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

        var response = await service.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // The response is wrapped in DataResponse<T> (ADR-049) -- the identifier is under
        // .data, not the response root.
        using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var identifier = created.RootElement.GetProperty("data").GetProperty("identifier").GetString();

        var stored = await ConfiguredCollection.Of<AppointmentEntity>(service, "AppointmentsCollection", "appointments")
            .Find(Builders<AppointmentEntity>.Filter.Eq(a => a.Identifier, identifier))
            .SingleOrDefaultAsync();

        Assert.NotNull(stored);
        Assert.Equal(ProviderEmail, stored.EmailProvider);
        Assert.Equal(CustomerEmail, stored.EmailCustomer);
        Assert.Equal(start, stored.Start);
        Assert.Equal(end, stored.End);
        Assert.False(stored.DayOff);
        Assert.Equal(AppointmentStatus.Requested, stored.AppointmentStatus);
    }
}
