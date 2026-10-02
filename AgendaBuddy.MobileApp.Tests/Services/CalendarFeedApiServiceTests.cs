using System.Net;
using System.Text;
using AgendaBuddy.MobileApp.Services;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

public class CalendarFeedApiServiceTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            return respond(request);
        }
    }

    private static (CalendarFeedApiService Sut, StubHandler Handler) Sut(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApi"))
            .Returns(() => new HttpClient(handler) { BaseAddress = new Uri("http://gateway.test/") });
        return (new CalendarFeedApiService(factory.Object), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task StatusReadsTheEnvelope()
    {
        var (sut, _) = Sut(_ => Json(HttpStatusCode.OK,
            """{"success":true,"data":{"enabled":true,"createdAt":"2026-10-01T12:00:00Z"},"errors":[]}"""));

        var status = await sut.GetStatusAsync();

        Assert.NotNull(status);
        Assert.True(status!.Enabled);
        Assert.NotNull(status.CreatedAt);
    }

    [Fact]
    public async Task AFailedStatusIsNullNotOff()
    {
        var (sut, _) = Sut(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        Assert.Null(await sut.GetStatusAsync());
    }

    [Fact]
    public async Task AnUnreachableServerIsNullNotOff()
    {
        var (sut, _) = Sut(_ => throw new HttpRequestException("down"));

        Assert.Null(await sut.GetStatusAsync());
        Assert.Null(await sut.EnableAsync("en"));
        Assert.False(await sut.DisableAsync());
    }

    [Fact]
    public async Task EnablePostsTheLanguageAndReturnsTheLink()
    {
        var (sut, handler) = Sut(_ => Json(HttpStatusCode.OK,
            """{"success":true,"data":{"url":"https://g.test/api/v1/calendar/feed/t.ics","createdAt":"2026-10-01T12:00:00Z"},"errors":[]}"""));

        var link = await sut.EnableAsync("es-MX");

        Assert.Equal("https://g.test/api/v1/calendar/feed/t.ics", link?.Url);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/calendar/feed", request.RequestUri!.AbsolutePath);
        Assert.Equal("""{"language":"es-MX"}""", body);
    }

    [Fact]
    public async Task AnEnableWithNoLinkInTheBodyIsAFailure()
    {
        var (sut, _) = Sut(_ => Json(HttpStatusCode.OK, """{"success":true,"data":{},"errors":[]}"""));

        Assert.Null(await sut.EnableAsync("en"));
    }

    [Fact]
    public async Task DisableSucceedsOnNoContent()
    {
        var (sut, handler) = Sut(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        Assert.True(await sut.DisableAsync());
        Assert.Equal(HttpMethod.Delete, Assert.Single(handler.Requests).Request.Method);
    }
}
