using System.Net;
using System.Text;
using AgendaBuddy.Library.Entities;
using MobileApp.Infrastructure;
using MobileApp.Models;
using MobileApp.Services;
using Moq;
using Xunit;

namespace MobileApp.Tests.Services;

public class BookingApiServiceTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static IHttpClientFactory CreateFactory(HttpStatusCode statusCode, string? jsonContent = null)
    {
        var content = jsonContent is not null
            ? new StringContent(jsonContent, Encoding.UTF8, "application/json")
            : new StringContent(string.Empty);

        var handler = new FakeHttpMessageHandler(statusCode, content);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApi")).Returns(client);
        return factory.Object;
    }

    private static AppointmentDetail Detail(string id, DateTime scheduledAt) => new()
    {
        Id = id,
        CustomerEmail = "alice@example.com",
        ProviderEmail = "prov@example.com",
        ScheduledAt = scheduledAt,
        Status = AppointmentStatus.Booked,
        ServiceId = "s1"
    };

    // ---------------------------------------------------------------------------
    // GetTodayAppointmentsAsync tests
    //
    // F-015-T07: Booking has no GET route for appointments (see the deviation note on
    // MobileApp.Routing.BookingRouteBuilder) — reads compose with ICalendarApiService instead.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetTodayAppointments_FiltersToTodayOnly()
    {
        var today = DateTime.UtcNow.Date.AddHours(9);
        var yesterday = today.AddDays(-1);

        var calendar = new Mock<ICalendarApiService>();
        calendar.Setup(c => c.GetAppointmentsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AppointmentDetail> { Detail("a1", today), Detail("a2", yesterday) });

        var sut = new BookingApiService(new Mock<IHttpClientFactory>().Object, calendar.Object);

        var result = await sut.GetTodayAppointmentsAsync();

        Assert.Single(result);
        Assert.Equal("a1", result[0].Id);
    }

    [Fact]
    public async Task GetTodayAppointments_CalendarReturnsEmpty_ReturnsEmptyList()
    {
        var calendar = new Mock<ICalendarApiService>();
        calendar.Setup(c => c.GetAppointmentsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AppointmentDetail>());

        var sut = new BookingApiService(new Mock<IHttpClientFactory>().Object, calendar.Object);

        var result = await sut.GetTodayAppointmentsAsync();

        Assert.Empty(result);
    }

    // ---------------------------------------------------------------------------
    // GetAppointmentAsync tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAppointment_FindsMatchingIdFromCalendarList()
    {
        var calendar = new Mock<ICalendarApiService>();
        calendar.Setup(c => c.GetAppointmentsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AppointmentDetail> { Detail("a1", DateTime.UtcNow) });

        var sut = new BookingApiService(new Mock<IHttpClientFactory>().Object, calendar.Object);

        var result = await sut.GetAppointmentAsync("a1");

        Assert.NotNull(result);
        Assert.Equal("a1", result!.Id);
    }

    [Fact]
    public async Task GetAppointment_NoMatch_ReturnsNull()
    {
        var calendar = new Mock<ICalendarApiService>();
        calendar.Setup(c => c.GetAppointmentsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AppointmentDetail> { Detail("a1", DateTime.UtcNow) });

        var sut = new BookingApiService(new Mock<IHttpClientFactory>().Object, calendar.Object);

        var result = await sut.GetAppointmentAsync("does-not-exist");

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // UpdateStatusAsync tests — F-015-T07 AC7: POST api/v1/booking/appointments/{id}/status,
    // replacing the legacy PUT booking/{id} call F-014 now ignores entirely.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateStatus_Returns200_RefetchesUpdatedAppointment()
    {
        var calendar = new Mock<ICalendarApiService>();
        calendar.Setup(c => c.GetAppointmentsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AppointmentDetail> { Detail("a1", DateTime.UtcNow) });

        var httpFactory = CreateFactory(HttpStatusCode.OK, """{"identifier":"a1","status":"Booked"}""");

        var sut = new BookingApiService(httpFactory, calendar.Object);

        var result = await sut.UpdateStatusAsync("a1", AppointmentStatus.Booked);

        Assert.NotNull(result);
        Assert.Equal("a1", result!.Id);
    }

    [Fact]
    public async Task UpdateStatus_Returns400_ReturnsNull()
    {
        // T-003: invalid status → API returns 400 → service returns null.
        var httpFactory = CreateFactory(HttpStatusCode.BadRequest);
        var sut = new BookingApiService(httpFactory, new Mock<ICalendarApiService>().Object);

        var result = await sut.UpdateStatusAsync("a1", AppointmentStatus.Confirmed);

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // F-014 session notes — new to the client (F-015-T07)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetNotes_Returns200_DeserializesList()
    {
        var json = """
            [{"_id":"64f0c2f1a1b2c3d4e5f6a7b8","id":"64f0c2f1a1b2c3d4e5f6a7b8","providerEmail":"prov@example.com","appointmentIdentifier":"a1","content":"Knee injury noted."}]
            """;

        var sut = new BookingApiService(CreateFactory(HttpStatusCode.OK, json), new Mock<ICalendarApiService>().Object);

        var result = await sut.GetNotesAsync("a1");

        Assert.Single(result);
        Assert.Equal("Knee injury noted.", result[0].Content);
    }

    [Fact]
    public async Task CreateNote_Returns201_DeserializesNote()
    {
        var json = """
            {"id":"64f0c2f1a1b2c3d4e5f6a7b8","providerEmail":"prov@example.com","appointmentIdentifier":"a1","content":"New note."}
            """;

        var sut = new BookingApiService(CreateFactory(HttpStatusCode.Created, json), new Mock<ICalendarApiService>().Object);

        var result = await sut.CreateNoteAsync("a1", "New note.");

        Assert.NotNull(result);
        Assert.Equal("New note.", result!.Content);
    }

    [Fact]
    public async Task CreateNote_Returns403_ReturnsNull()
    {
        var sut = new BookingApiService(CreateFactory(HttpStatusCode.Forbidden), new Mock<ICalendarApiService>().Object);

        var result = await sut.CreateNoteAsync("a1", "New note.");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateNote_Returns200_DeserializesNote()
    {
        var json = """
            {"id":"64f0c2f1a1b2c3d4e5f6a7b8","providerEmail":"prov@example.com","appointmentIdentifier":"a1","content":"Updated."}
            """;

        var sut = new BookingApiService(CreateFactory(HttpStatusCode.OK, json), new Mock<ICalendarApiService>().Object);

        var result = await sut.UpdateNoteAsync("64f0c2f1a1b2c3d4e5f6a7b8", "Updated.");

        Assert.NotNull(result);
        Assert.Equal("Updated.", result!.Content);
    }

    // ---------------------------------------------------------------------------
    // Payments — new to the client (F-015-T07)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetPayment_Returns200_DeserializesPayment()
    {
        var json = """
            {"id":"64f0c2f1a1b2c3d4e5f6a7b8","appointmentIdentifier":"a1","providerEmail":"prov@example.com","customerEmail":"alice@example.com","amount":50,"currency":"usd","status":1}
            """;

        var sut = new BookingApiService(CreateFactory(HttpStatusCode.OK, json), new Mock<ICalendarApiService>().Object);

        var result = await sut.GetPaymentAsync("a1");

        Assert.NotNull(result);
        Assert.Equal(50, result!.Amount);
    }

    [Fact]
    public async Task GetPayment_Returns404_ReturnsNull()
    {
        var sut = new BookingApiService(CreateFactory(HttpStatusCode.NotFound), new Mock<ICalendarApiService>().Object);

        var result = await sut.GetPaymentAsync("does-not-exist");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreatePayment_Returns201_DeserializesPayment()
    {
        var json = """
            {"id":"64f0c2f1a1b2c3d4e5f6a7b8","appointmentIdentifier":"a1","providerEmail":"prov@example.com","customerEmail":"alice@example.com","amount":75,"currency":"usd","status":1}
            """;

        var sut = new BookingApiService(CreateFactory(HttpStatusCode.Created, json), new Mock<ICalendarApiService>().Object);

        var result = await sut.CreatePaymentAsync("a1", 75m, "usd");

        Assert.NotNull(result);
        Assert.Equal(75, result!.Amount);
    }

    [Fact]
    public async Task CreatePayment_Returns409_ReturnsNull()
    {
        var sut = new BookingApiService(CreateFactory(HttpStatusCode.Conflict), new Mock<ICalendarApiService>().Object);

        var result = await sut.CreatePaymentAsync("a1", 75m, "usd");

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // ux-review.md finding 2 / api-contracts.md §1: a gateway-shaped failure (carrying failedService)
    // is distinguished from a plain domain 4xx (empty/non-gateway body) — the latter still just
    // returns null, unchanged from before.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateStatus_GatewayFailure_ThrowsWithFailedService()
    {
        const string json = """{"type":"https://agendabuddy.dev/errors/gateway-destination-unreachable","status":502,"failedService":"booking"}""";
        var sut = new BookingApiService(CreateFactory(HttpStatusCode.BadGateway, json), new Mock<ICalendarApiService>().Object);

        var ex = await Assert.ThrowsAsync<GatewayServiceUnavailableException>(
            () => sut.UpdateStatusAsync("a1", AppointmentStatus.Completed));

        Assert.Equal("booking", ex.FailedService);
    }

    [Fact]
    public async Task GetPayment_GatewayFailure_ThrowsWithFailedService()
    {
        const string json = """{"failedService":"booking"}""";
        var sut = new BookingApiService(CreateFactory(HttpStatusCode.BadGateway, json), new Mock<ICalendarApiService>().Object);

        var ex = await Assert.ThrowsAsync<GatewayServiceUnavailableException>(
            () => sut.GetPaymentAsync("a1"));

        Assert.Equal("booking", ex.FailedService);
    }

    // ---------------------------------------------------------------------------
    // Fake handler
    // ---------------------------------------------------------------------------

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly HttpContent _content;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, HttpContent content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode) { Content = _content });
        }
    }
}
