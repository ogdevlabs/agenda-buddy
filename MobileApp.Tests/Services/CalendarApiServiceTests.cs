using System.Net;
using System.Net.Http;
using System.Text;
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
        var sut = new CalendarApiService(factory);

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
        var sut = new CalendarApiService(factory);

        var result = await sut.GetAvailabilityAsync();

        Assert.NotNull(result);
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
