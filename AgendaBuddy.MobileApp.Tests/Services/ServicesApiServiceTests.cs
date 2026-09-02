using System.Net;
using System.Text;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Services;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

public class ServicesApiServiceTests
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

    [Fact]
    public async Task GetServices_Returns200_ParsesDurationAndIsActive_SkipsBrokenIdField()
    {
        var json = """
            {
                "data": [
                    {"id":{"timestamp":1,"machine":2,"pid":3,"increment":4,"creationTime":"2026-08-01T00:00:00Z"},
                     "name":"Consultation","description":"30 min","fee":50,"feeType":0,"isActive":false,"durationMinutes":30}
                ],
                "errors": []
            }
            """;

        var sut = new ServicesApiService(CreateFactory(HttpStatusCode.OK, json));

        var result = await sut.GetServicesAsync("provider@example.com");

        Assert.Single(result);
        Assert.Equal("Consultation", result[0].Name);
        Assert.Equal(30, result[0].DurationMinutes);
        Assert.False(result[0].IsActive);
    }

    [Fact]
    public async Task GetServices_MissingDurationField_ParsesAsNull()
    {
        var json = """{"data": [{"name":"Massage","description":"desc","fee":80,"feeType":0,"isActive":true}], "errors": []}""";

        var sut = new ServicesApiService(CreateFactory(HttpStatusCode.OK, json));

        var result = await sut.GetServicesAsync("provider@example.com");

        Assert.Null(result[0].DurationMinutes);
    }

    [Fact]
    public async Task GetServices_ExplicitNullDurationAndFee_ParsesAsNullWithoutThrowing()
    {
        // Regression test: JsonElement.TryGetInt32/TryGetDecimal throw InvalidOperationException for a
        // JSON `null` value (as opposed to a missing property, which TryGetProperty handles). A service
        // saved with no duration/fee set round-trips as an explicit `null` on the wire, not an absent
        // field, and crashed the whole services list load before GetInt/GetDecimal gained a ValueKind
        // guard.
        var json = """{"data": [{"name":"Sweep Test","description":"desc","fee":null,"feeType":0,"isActive":true,"durationMinutes":null}], "errors": []}""";

        var sut = new ServicesApiService(CreateFactory(HttpStatusCode.OK, json));

        var result = await sut.GetServicesAsync("provider@example.com");

        Assert.Single(result);
        Assert.Null(result[0].DurationMinutes);
        Assert.Null(result[0].Fee);
    }

    [Fact]
    public async Task RemoveService_Returns200_ReturnsTrue()
    {
        var sut = new ServicesApiService(CreateFactory(HttpStatusCode.OK, """{"data":{},"errors":[]}"""));

        var result = await sut.RemoveServiceAsync("provider@example.com", "Massage");

        Assert.True(result);
    }

    [Fact]
    public async Task RemoveService_Returns404_ReturnsFalse()
    {
        var sut = new ServicesApiService(CreateFactory(HttpStatusCode.NotFound));

        var result = await sut.RemoveServiceAsync("provider@example.com", "Nonexistent");

        Assert.False(result);
    }

    [Fact]
    public async Task RemoveService_NameWithSpaces_UrlEncodesPath()
    {
        HttpRequestMessage? captured = null;
        var handler = new CapturingHandler(req => captured = req, HttpStatusCode.OK, """{"data":{},"errors":[]}""");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApi")).Returns(client);
        var sut = new ServicesApiService(factory.Object);

        await sut.RemoveServiceAsync("provider@example.com", "Personal Training Session");

        Assert.NotNull(captured);
        Assert.DoesNotContain(" ", captured!.RequestUri!.AbsoluteUri);
        Assert.Contains("Personal%20Training%20Session", captured.RequestUri!.AbsoluteUri);
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

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Action<HttpRequestMessage> _capture;
        private readonly HttpStatusCode _statusCode;
        private readonly string _json;

        public CapturingHandler(Action<HttpRequestMessage> capture, HttpStatusCode statusCode, string json)
        {
            _capture = capture;
            _statusCode = statusCode;
            _json = json;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _capture(request);
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }
}
