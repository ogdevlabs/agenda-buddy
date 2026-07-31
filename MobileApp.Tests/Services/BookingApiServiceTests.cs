using System.Net;
using System.Text;
using Library.Entities;
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

    // ---------------------------------------------------------------------------
    // GetTodayAppointmentsAsync tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetTodayAppointments_Returns200_DeserializesList()
    {
        var json = """
            [
                {"id":"a1","customerEmail":"alice@example.com","providerEmail":"prov@example.com","scheduledAt":"2026-07-31T09:00:00Z","status":0,"serviceId":"s1"},
                {"id":"a2","customerEmail":"bob@example.com","providerEmail":"prov@example.com","scheduledAt":"2026-07-31T10:00:00Z","status":1,"serviceId":"s2"}
            ]
            """;

        var sut = new BookingApiService(CreateFactory(HttpStatusCode.OK, json));

        var result = await sut.GetTodayAppointmentsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("alice@example.com", result[0].CustomerEmail);
        Assert.Equal("bob@example.com", result[1].CustomerEmail);
    }

    [Fact]
    public async Task GetTodayAppointments_Returns401_ReturnsEmptyList()
    {
        var sut = new BookingApiService(CreateFactory(HttpStatusCode.Unauthorized));

        var result = await sut.GetTodayAppointmentsAsync();

        Assert.Empty(result);
    }

    // ---------------------------------------------------------------------------
    // GetAppointmentAsync tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAppointment_Returns200_DeserializesAppointment()
    {
        var json = """
            {"id":"a1","customerEmail":"alice@example.com","providerEmail":"prov@example.com","scheduledAt":"2026-07-31T09:00:00Z","status":1,"serviceId":"s1"}
            """;

        var sut = new BookingApiService(CreateFactory(HttpStatusCode.OK, json));

        var result = await sut.GetAppointmentAsync("a1");

        Assert.NotNull(result);
        Assert.Equal("a1", result!.Id);
        Assert.Equal("alice@example.com", result.CustomerEmail);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
    }

    [Fact]
    public async Task GetAppointment_Returns404_ReturnsNull()
    {
        var sut = new BookingApiService(CreateFactory(HttpStatusCode.NotFound));

        var result = await sut.GetAppointmentAsync("does-not-exist");

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // UpdateStatusAsync tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateStatus_Returns200_ReturnsUpdatedAppointment()
    {
        var json = """
            {"id":"a1","customerEmail":"alice@example.com","providerEmail":"prov@example.com","scheduledAt":"2026-07-31T09:00:00Z","status":1,"serviceId":"s1"}
            """;

        var sut = new BookingApiService(CreateFactory(HttpStatusCode.OK, json));

        var result = await sut.UpdateStatusAsync("a1", AppointmentStatus.Booked);

        Assert.NotNull(result);
        Assert.Equal("a1", result!.Id);
        Assert.Equal(AppointmentStatus.Booked, result.Status);
    }

    [Fact]
    public async Task UpdateStatus_Returns400_ReturnsNull()
    {
        // T-003: invalid status → API returns 400 → service returns null.
        var sut = new BookingApiService(CreateFactory(HttpStatusCode.BadRequest));

        var result = await sut.UpdateStatusAsync("a1", AppointmentStatus.Confirmed);

        Assert.Null(result);
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
