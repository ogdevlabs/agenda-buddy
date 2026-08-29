using System.Net;
using System.Net.Http;
using System.Text;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Services;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

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

    /// <summary>
    /// GetAvailabilityAsync composes with GetAppointmentsAsync (same "AgendaBuddyApi" client) to fold in
    /// booked slots, so a test exercising it needs distinct bodies per route rather than one fixed response.
    /// </summary>
    private static IHttpClientFactory CreateFactoryPerRoute(Dictionary<string, string> jsonByRouteFragment)
    {
        var handler = new RoutingFakeHttpMessageHandler(jsonByRouteFragment);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApi")).Returns(client);
        return factory.Object;
    }

    private static IUserSessionService CreateSession(string email = "alice@example.com")
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(email);
        // Defaults to a Provider session — matches the existing appointment fixtures below, where
        // "alice@example.com" is the caller viewing their own calendar as EmailProvider's counterpart.
        session.SetupGet(s => s.IsProvider).Returns(true);
        return session.Object;
    }

    // The real endpoint (AgendaBuddy.Calendar.Api/Modules/CalendarModule.cs) wraps its body in
    // DataResponse<List<DateTime>> and returns a flat list of free slots, not the day-grouped
    // {date,availableSlots,bookedSlots} shape docs/pdlc/design/mobile-app/api-contracts.md describes.
    // GetAvailabilityAsync builds one entry per day in [today, today+days) — not just the dates the flat
    // slot list happens to mention — so the day-selector strip always gets a contiguous run of tiles, and
    // composes with GetAppointmentsAsync (same "AgendaBuddyApi" client) to fill in BookedSlots.
    [Fact]
    public async Task GetAvailability_Returns200_BuildsContiguousDayRangeAndFoldsInBookedAppointments()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var availabilityJson = $$"""
            {
                "data": ["{{today:yyyy-MM-dd}}T09:00:00Z", "{{today:yyyy-MM-dd}}T10:00:00Z", "{{tomorrow:yyyy-MM-dd}}T09:00:00Z"],
                "errors": []
            }
            """;
        var appointmentsJson = $$"""
            {
                "data": [
                    {
                        "identifier": "a1",
                        "emailProvider": "prov@example.com",
                        "emailCustomer": "bob@example.com",
                        "start": "{{today:yyyy-MM-dd}}T11:00:00Z",
                        "appointmentStatus": 1
                    }
                ],
                "errors": []
            }
            """;

        var factory = CreateFactoryPerRoute(new Dictionary<string, string>
        {
            ["availability"] = availabilityJson,
            ["appointments"] = appointmentsJson
        });
        var sut = new CalendarApiService(factory, CreateSession());

        var result = await sut.GetAvailabilityAsync(7);

        // Every one of the 7 requested days gets an entry, not just the 2 dates with data.
        Assert.Equal(7, result.Count);
        Assert.Equal(today.ToString("yyyy-MM-dd"), result[0].Date);
        Assert.Equal(2, result[0].AvailableSlots.Count);
        Assert.Single(result[0].BookedSlots);
        Assert.Contains("bob@example.com", result[0].BookedSlots[0]);
        Assert.Equal(tomorrow.ToString("yyyy-MM-dd"), result[1].Date);
        Assert.Single(result[1].AvailableSlots);
        Assert.Empty(result[1].BookedSlots);
        Assert.Empty(result[2].AvailableSlots);
        Assert.Empty(result[2].BookedSlots);
    }

    [Fact]
    public async Task GetAvailability_CustomerSession_BookedSlotLabelShowsProviderEmail()
    {
        var today = DateTime.Today;

        var availabilityJson = """{"data": [], "errors": []}""";
        var appointmentsJson = $$"""
            {
                "data": [
                    {
                        "identifier": "a1",
                        "emailProvider": "prov@example.com",
                        "emailCustomer": "alice@example.com",
                        "start": "{{today:yyyy-MM-dd}}T11:00:00Z",
                        "appointmentStatus": 1
                    }
                ],
                "errors": []
            }
            """;

        var factory = CreateFactoryPerRoute(new Dictionary<string, string>
        {
            ["availability"] = availabilityJson,
            ["appointments"] = appointmentsJson
        });
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns("alice@example.com");
        session.SetupGet(s => s.IsProvider).Returns(false);
        var sut = new CalendarApiService(factory, session.Object);

        var result = await sut.GetAvailabilityAsync(1);

        Assert.Single(result[0].BookedSlots);
        Assert.Contains("prov@example.com", result[0].BookedSlots[0]);
    }

    // ParseAppointments is static and role-blind, so it leaves DisplayName empty — which rendered a
    // nameless row wherever an appointment is listed. GetAppointmentsAsync fills it from the session role.
    [Theory]
    [InlineData(true, "bob@example.com")]   // Provider sees their customer
    [InlineData(false, "prov@example.com")] // Customer sees their provider
    public async Task GetAppointments_SetsDisplayNameToTheCounterpartForTheSessionRole(bool isProvider, string expected)
    {
        var json = $$"""
            {
                "data": [
                    {
                        "identifier": "a1",
                        "emailProvider": "prov@example.com",
                        "emailCustomer": "bob@example.com",
                        "start": "{{DateTime.Today:yyyy-MM-dd}}T11:00:00Z",
                        "appointmentStatus": 1
                    }
                ],
                "errors": []
            }
            """;

        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(isProvider ? "prov@example.com" : "bob@example.com");
        session.SetupGet(s => s.IsProvider).Returns(isProvider);
        session.SetupGet(s => s.IsCustomer).Returns(!isProvider);
        var sut = new CalendarApiService(CreateFactory(HttpStatusCode.OK, json), session.Object);

        var result = await sut.GetAppointmentsAsync();

        Assert.Equal(expected, Assert.Single(result).DisplayName);
        Assert.Equal(expected, result[0].ContactEmail);
    }

    [Fact]
    public async Task GetAvailability_Returns401_StillBuildsEmptyDayTilesRatherThanAnEmptyList()
    {
        // A failed availability call must not collapse the day-selector strip to nothing — same
        // reasoning as the 404-for-a-Customer case this mirrors (see GetAvailabilityAsync's remarks).
        var factory = CreateFactory(HttpStatusCode.Unauthorized);
        var sut = new CalendarApiService(factory, CreateSession());

        var result = await sut.GetAvailabilityAsync(3);

        Assert.Equal(3, result.Count);
        Assert.All(result, day => Assert.Empty(day.AvailableSlots));
        Assert.All(result, day => Assert.Empty(day.BookedSlots));
    }

    [Fact]
    public async Task GetAvailability_CustomerSession_SkipsTheAvailabilityCallAndStillBuildsDayTilesFromAppointments()
    {
        // "Availability" is a Provider concept a Customer's own email has none of — CalendarModule.cs's
        // /availability/{email} route 404s for a Customer by design, which used to bail this method out
        // with zero day-tiles instead of a real (if slot-less) calendar. See GetAvailabilityAsync's remarks.
        var today = DateTime.Today;
        var appointmentsJson = $$"""
            {
                "data": [
                    {
                        "identifier": "a1",
                        "emailProvider": "prov@example.com",
                        "emailCustomer": "alice@example.com",
                        "start": "{{today:yyyy-MM-dd}}T11:00:00Z",
                        "appointmentStatus": 1
                    }
                ],
                "errors": []
            }
            """;

        var factory = CreateFactoryPerRoute(new Dictionary<string, string>
        {
            ["appointments"] = appointmentsJson
            // Deliberately no "availability" entry — a Customer session must never call that route.
        });
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns("alice@example.com");
        session.SetupGet(s => s.IsProvider).Returns(false);
        session.SetupGet(s => s.IsCustomer).Returns(true);
        var sut = new CalendarApiService(factory, session.Object);

        var result = await sut.GetAvailabilityAsync(3);

        Assert.Equal(3, result.Count);
        Assert.Single(result[0].BookedSlots);
        Assert.Contains("prov@example.com", result[0].BookedSlots[0]);
        Assert.All(result, day => Assert.Empty(day.AvailableSlots));
    }

    // ---------------------------------------------------------------------------
    // GetAppointmentsAsync — the real read path BookingApiService composes with this.
    // ---------------------------------------------------------------------------

    // The real endpoint wraps its body in DataResponse<List<AppointmentEntity>> ({"data": [...], "errors": []}),
    // not a bare array root.
    [Fact]
    public async Task GetAppointments_Returns200_MapsIdentifierEmailsAndStatus()
    {
        const string json = """
            {
                "data": [
                    {
                        "identifier": "a1",
                        "emailProvider": "prov@example.com",
                        "emailCustomer": "alice@example.com",
                        "start": "2026-07-31T09:00:00Z",
                        "end": "2026-07-31T09:30:00Z",
                        "appointmentStatus": 1
                    }
                ],
                "errors": []
            }
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
            {
                "data": [
                    {
                        "id": {"timestamp": 1787455661, "machine": 12345, "pid": 678, "increment": 90, "creationTime": "2026-08-01T00:00:00Z"},
                        "identifier": "a1",
                        "emailProvider": "prov@example.com",
                        "emailCustomer": "alice@example.com",
                        "start": "2026-07-31T09:00:00Z",
                        "appointmentStatus": 0
                    }
                ],
                "errors": []
            }
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

    private sealed class RoutingFakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _jsonByRouteFragment;

        public RoutingFakeHttpMessageHandler(Dictionary<string, string> jsonByRouteFragment)
        {
            _jsonByRouteFragment = jsonByRouteFragment;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var match = _jsonByRouteFragment.First(kv => path.Contains(kv.Key, StringComparison.OrdinalIgnoreCase));

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(match.Value, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
