using System.Net;
using System.Net.Http;
using System.Text;
using Library.Entities;
using MobileApp.Services;
using Moq;
using Xunit;

namespace MobileApp.Tests.Services;

public class CalendarApiServiceTests
{
    private static IHttpClientFactory CreateFactory(HttpStatusCode statusCode, string? jsonContent = null)
    {
        var content = jsonContent is not null
            ? new StringContent(jsonContent, Encoding.UTF8, "application/json")
            : null;

        var handler = new FakeHttpMessageHandler(statusCode, content);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApi")).Returns(client);
        return factory.Object;
    }

    private static IUserSessionService CreateSession(string email = "alice@example.com")
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(email);
        return session.Object;
    }

    [Fact]
    public async Task GetAvailability_Returns200_DeserializesList()
    {
        const string json = """
            [
                {
                    "date": "2026-08-01",
                    "availableSlots": ["09:00", "10:00"],
                    "bookedSlots": ["11:00"]
                },
                {
                    "date": "2026-08-02",
                    "availableSlots": [],
                    "bookedSlots": ["09:00", "10:00", "11:00"]
                }
            ]
            """;

        var factory = CreateFactory(HttpStatusCode.OK, json);
        var sut = new CalendarApiService(factory, CreateSession());

        var result = await sut.GetAvailabilityAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("2026-08-01", result[0].Date);
        Assert.Equal(2, result[0].AvailableSlots.Count);
        Assert.Single(result[0].BookedSlots);
        Assert.Equal("2026-08-02", result[1].Date);
        Assert.Empty(result[1].AvailableSlots);
        Assert.Equal(3, result[1].BookedSlots.Count);
    }

    [Fact]
    public async Task GetAvailability_Returns401_ReturnsEmptyList()
    {
        var factory = CreateFactory(HttpStatusCode.Unauthorized);
        var sut = new CalendarApiService(factory, CreateSession());

        var result = await sut.GetAvailabilityAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ---------------------------------------------------------------------------
    // GetAppointmentsAsync — F-015-T07: new. Also the real read path BookingApiService composes with.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAppointments_Returns200_MapsIdentifierEmailsAndStatus()
    {
        const string json = """
            [
                {
                    "identifier": "a1",
                    "emailProvider": "prov@example.com",
                    "emailCustomer": "alice@example.com",
                    "start": "2026-07-31T09:00:00Z",
                    "end": "2026-07-31T09:30:00Z",
                    "appointmentStatus": 1
                }
            ]
            """;

        var sut = new CalendarApiService(CreateFactory(HttpStatusCode.OK, json), CreateSession());

        var result = await sut.GetAppointmentsAsync();

        Assert.Single(result);
        Assert.Equal("a1", result[0].Id);
        Assert.Equal("prov@example.com", result[0].ProviderEmail);
        Assert.Equal("alice@example.com", result[0].CustomerEmail);
        Assert.Equal(AppointmentStatus.Booked, result[0].Status);
    }

    // Calendar does not register ObjectIdJsonConverter (filed, pre-existing — Booking/Customer/Provider do).
    // Its `id`/`_id` field is emitted as the broken {timestamp,machine,...} shape. This must not crash the
    // client; the parser never touches that field, using `identifier` instead.
    [Fact]
    public async Task GetAppointments_BrokenMongoIdShape_DoesNotThrow()
    {
        const string json = """
            [
                {
                    "id": {"timestamp": 1787455661, "machine": 12345, "pid": 678, "increment": 90, "creationTime": "2026-08-01T00:00:00Z"},
                    "identifier": "a1",
                    "emailProvider": "prov@example.com",
                    "emailCustomer": "alice@example.com",
                    "start": "2026-07-31T09:00:00Z",
                    "appointmentStatus": 0
                }
            ]
            """;

        var sut = new CalendarApiService(CreateFactory(HttpStatusCode.OK, json), CreateSession());

        var result = await sut.GetAppointmentsAsync();

        Assert.Single(result);
        Assert.Equal("a1", result[0].Id);
    }

    [Fact]
    public async Task GetAppointments_Returns401_ReturnsEmptyList()
    {
        var sut = new CalendarApiService(CreateFactory(HttpStatusCode.Unauthorized), CreateSession());

        var result = await sut.GetAppointmentsAsync();

        Assert.Empty(result);
    }

    // ---------------------------------------------------------------------------
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly HttpContent? _content;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, HttpContent? content = null)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = _content ?? new StringContent(string.Empty)
            };
            return Task.FromResult(response);
        }
    }
}
