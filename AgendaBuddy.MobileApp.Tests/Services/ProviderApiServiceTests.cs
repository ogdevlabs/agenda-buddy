using System.Net;
using System.Text;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

public class ProviderApiServiceTests
{
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

    private static IUserSessionService CreateSession(string email = "provider@example.com")
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(email);
        return session.Object;
    }

    // F-015-T07: new — F-014's provider report route, never called by the client before this task.
    [Fact]
    public async Task GetReport_Returns200_DeserializesReport()
    {
        const string json = """
            {
                "providerEmail": "provider@example.com",
                "totalBookings": 10,
                "completedAppointments": 6,
                "cancelledAppointments": 1,
                "uniqueCustomers": 4,
                "retentionRate": 0.5,
                "revenueAvailable": false,
                "revenueUnavailableReason": "Appointments do not record which service they were booked for."
            }
            """;

        var sut = new ProviderApiService(CreateFactory(HttpStatusCode.OK, json), CreateSession());

        var result = await sut.GetReportAsync();

        Assert.NotNull(result);
        Assert.False(result!.RevenueAvailable);
        Assert.Equal("provider@example.com", result.ProviderEmail);
    }

    [Fact]
    public async Task GetReport_Returns404_ReturnsNull()
    {
        var sut = new ProviderApiService(CreateFactory(HttpStatusCode.NotFound), CreateSession());

        var result = await sut.GetReportAsync();

        Assert.Null(result);
    }

    // ux-review.md finding 2 / api-contracts.md §1: a gateway-shaped 5xx (carrying failedService) is
    // distinguished from a plain domain 404 (empty body) — the latter still just returns null.
    [Fact]
    public async Task GetReport_GatewayFailure_ThrowsWithFailedService()
    {
        const string json = """{"failedService":"provider"}""";
        var sut = new ProviderApiService(CreateFactory(HttpStatusCode.BadGateway, json), CreateSession());

        var ex = await Assert.ThrowsAsync<GatewayServiceUnavailableException>(() => sut.GetReportAsync());

        Assert.Equal("provider", ex.FailedService);
    }

    // F-015-T07: new — F-014's provider deactivation route, never called by the client before this task.
    [Fact]
    public async Task Deactivate_Returns202_ReturnsTrue()
    {
        var sut = new ProviderApiService(CreateFactory(HttpStatusCode.Accepted), CreateSession());

        var result = await sut.DeactivateAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task Deactivate_Returns403_ReturnsFalse()
    {
        var sut = new ProviderApiService(CreateFactory(HttpStatusCode.Forbidden), CreateSession());

        var result = await sut.DeactivateAsync();

        Assert.False(result);
    }

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
