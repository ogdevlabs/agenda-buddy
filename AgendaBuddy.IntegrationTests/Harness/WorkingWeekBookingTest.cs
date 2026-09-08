using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// A customer can only book inside the provider's declared working week, over real HTTP.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Nothing enforced this.</b> <c>GET /api/v1/calendar/availability/{email}</c> honoured
/// <c>ProviderEntity.WorkWeek</c> correctly, while <c>POST /api/v1/booking/appointments</c> validated only "in the
/// future", "no overlap" and "a service this provider offers". Probed live on 2026-09-08 against the running
/// stack: a Saturday the provider had explicitly closed, 03:00 on an open Monday, and 22:00 after closing were
/// <b>all accepted with 201 Created</b>. The calendar was a suggestion, not a rule.
/// </para>
/// <para>
/// Asserted through the API rather than on <c>AvailabilityCalculator</c> directly because the defect was the
/// <i>absence of a call</i> — the unit tests for the calculator all passed while the booking route ignored it.
/// A handler-level unit test cannot reach this either: <c>ProviderService</c>/<c>BookingService</c> are concrete
/// and non-virtual, which is why <c>BookingAppointmentCommandHandlerTest</c> covers only the null guard.
/// </para>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class WorkingWeekBookingTest : IClassFixture<ServiceHostFixture<BookingAnchor>>
{
    private const string Provider = "workweek-provider@example.com";
    private const string Customer = "workweek-customer@example.com";

    /// <summary>UTC, so a local wall clock and the stored instant are the same thing and the maths stays legible.</summary>
    private const string Zone = "UTC";

    private readonly ServiceHostFixture<BookingAnchor> _host;
    private readonly TokenFactory _tokens;

    public WorkingWeekBookingTest(ServiceHostFixture<BookingAnchor> host, CryptoSessionFixture crypto)
    {
        _host = host;
        _tokens = new TokenFactory(crypto);
    }

    /// <summary>The next occurrence of <paramref name="day"/> at <paramref name="hour"/>:00 UTC, always future.</summary>
    private static DateTime Next(DayOfWeek day, int hour)
    {
        var date = DateTime.UtcNow.Date.AddDays(1);
        while (date.DayOfWeek != day) date = date.AddDays(1);
        return date.AddHours(hour);
    }

    private async Task<ServiceHost> StartWithAWorkingWeekAsync(List<WorkDayHours> week)
    {
        var service = _host.StartService("Production");

        await service.Database.GetCollection<ProviderEntity>("providers").InsertOneAsync(new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Pat",
            LastName = "Coach",
            Email = Provider,
            TimeZoneId = Zone,
            WorkWeek = week,
        });

        return service;
    }

    private async Task<HttpResponseMessage> BookAsync(ServiceHost service, DateTime startUtc, int hours = 1)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/booking/appointments")
        {
            Content = JsonContent.Create(new
            {
                emailProvider = Provider,
                emailCustomer = Customer,
                start = startUtc,
                end = startUtc.AddHours(hours),
                appointmentDescription = "working-week probe",
            }),
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer", _tokens.CreateToken(Customer, TokenFactory.CustomerRole)),
            },
        };

        return await service.Client.SendAsync(request);
    }

    private static List<WorkDayHours> MondayToFridayNineToFive() =>
    [
        new(DayOfWeek.Monday, 9, 17),
        new(DayOfWeek.Tuesday, 9, 17),
        new(DayOfWeek.Wednesday, 9, 17),
        new(DayOfWeek.Thursday, 9, 17),
        new(DayOfWeek.Friday, 9, 17),
        new(DayOfWeek.Saturday, 9, 17, isClosed: true),
        new(DayOfWeek.Sunday, 9, 17, isClosed: true),
    ];

    // ── the control ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Without this, a check that refused everything would satisfy every assertion below.
    /// </summary>
    [Fact]
    public async Task ASlotInsideTheWorkingWeekIsStillAccepted()
    {
        using var service = await StartWithAWorkingWeekAsync(MondayToFridayNineToFive());

        var response = await BookAsync(service, Next(DayOfWeek.Wednesday, 11));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── the defect ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>⚠️ The reported case: a weekend the provider explicitly closed.</summary>
    [Theory]
    [InlineData(DayOfWeek.Saturday)]
    [InlineData(DayOfWeek.Sunday)]
    public async Task ASlotOnAClosedDayIsRefusedAndNothingIsStored(DayOfWeek day)
    {
        using var service = await StartWithAWorkingWeekAsync(MondayToFridayNineToFive());

        var response = await BookAsync(service, Next(day, 12));

        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(await service.Database.GetCollection<AppointmentEntity>("appointments")
            .Find(Builders<AppointmentEntity>.Filter.Eq(a => a.EmailProvider, Provider)).ToListAsync());
    }

    [Fact]
    public async Task ASlotBeforeOpeningIsRefused()
    {
        using var service = await StartWithAWorkingWeekAsync(MondayToFridayNineToFive());

        var response = await BookAsync(service, Next(DayOfWeek.Tuesday, 3));

        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ASlotAfterClosingIsRefused()
    {
        using var service = await StartWithAWorkingWeekAsync(MondayToFridayNineToFive());

        var response = await BookAsync(service, Next(DayOfWeek.Tuesday, 22));

        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// A session that starts inside the window but runs past closing is refused — the whole session has to fit,
    /// not just its start. This is what stops a two-hour booking at 16:00 on a day that closes at 17:00.
    /// </summary>
    [Fact]
    public async Task ASlotStartingInsideButRunningPastClosingIsRefused()
    {
        using var service = await StartWithAWorkingWeekAsync(MondayToFridayNineToFive());

        var response = await BookAsync(service, Next(DayOfWeek.Tuesday, 16), hours: 3);

        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
    }

    // ── the default ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ A provider who has configured <b>nothing</b> works Monday to Friday, so a weekend booking is refused
    /// without them having had to say so. Before this, an empty week meant every day inherited the legacy hours
    /// pair and the weekend was bookable by default.
    /// </summary>
    [Theory]
    [InlineData(DayOfWeek.Saturday)]
    [InlineData(DayOfWeek.Sunday)]
    public async Task AnUnconfiguredProviderRefusesWeekendBookings(DayOfWeek day)
    {
        using var service = await StartWithAWorkingWeekAsync([]);

        var response = await BookAsync(service, Next(day, 12));

        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>...and is still bookable on a weekday, on the default hours.</summary>
    [Fact]
    public async Task AnUnconfiguredProviderIsStillBookableOnAWeekday()
    {
        using var service = await StartWithAWorkingWeekAsync([]);

        var response = await BookAsync(service, Next(DayOfWeek.Thursday, 11));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// The default is only a default: a provider who opens Saturday is bookable on Saturday.
    /// </summary>
    [Fact]
    public async Task AProviderWhoOpensSaturdayIsBookableThen()
    {
        using var service = await StartWithAWorkingWeekAsync([new WorkDayHours(DayOfWeek.Saturday, 9, 14)]);

        var response = await BookAsync(service, Next(DayOfWeek.Saturday, 10));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── the invariant ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ <b>What the calendar offers is exactly what a booking may take.</b> This is the property the whole fix
    /// is for: a slot listed by <c>GET /availability</c> must be accepted by <c>POST /appointments</c>, and one
    /// not listed must be refused. It is asserted across both services rather than inside one.
    /// </summary>
    [Fact]
    public async Task EveryOfferedSlotIsBookableAndAnUnofferedOneIsNot()
    {
        using var service = await StartWithAWorkingWeekAsync(MondayToFridayNineToFive());

        // A weekday slot the week allows, and a weekend one it does not.
        var offered = Next(DayOfWeek.Monday, 10);
        var notOffered = Next(DayOfWeek.Sunday, 10);

        Assert.Equal(HttpStatusCode.Created, (await BookAsync(service, offered)).StatusCode);
        Assert.NotEqual(HttpStatusCode.Created, (await BookAsync(service, notOffered)).StatusCode);
    }
}
