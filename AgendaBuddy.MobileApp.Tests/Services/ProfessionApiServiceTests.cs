using System.Net;
using System.Text;
using AgendaBuddy.MobileApp.Services;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

public class ProfessionApiServiceTests
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
    public async Task GetProfessions_Returns200_ParsesNames()
    {
        var json = """{"data":[{"name":"Coaching"},{"name":"Tutoring"}],"errors":[]}""";
        var sut = new ProfessionApiService(CreateFactory(HttpStatusCode.OK, json));

        var result = await sut.GetProfessionsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Coaching", result[0].Name);
        Assert.False(result[0].IsSelected);
    }

    [Fact]
    public async Task GetProviderProfessions_Returns200_ParsesStringList()
    {
        var json = """{"data":["Coaching","Tutoring"],"errors":[]}""";
        var sut = new ProfessionApiService(CreateFactory(HttpStatusCode.OK, json));

        var result = await sut.GetProviderProfessionsAsync("pat@test.dev");

        Assert.Equal(["Coaching", "Tutoring"], result);
    }

    [Fact]
    public async Task GetProviderProfessions_NonSuccess_ReturnsEmptyList()
    {
        var sut = new ProfessionApiService(CreateFactory(HttpStatusCode.InternalServerError));

        var result = await sut.GetProviderProfessionsAsync("pat@test.dev");

        Assert.Empty(result);
    }

    [Fact]
    public async Task AddProfessionsToProvider_Returns200_ReturnsTrue()
    {
        var sut = new ProfessionApiService(CreateFactory(HttpStatusCode.OK, """{"data":["Coaching"],"errors":[]}"""));

        var result = await sut.AddProfessionsToProviderAsync("pat@test.dev", ["Coaching"]);

        Assert.True(result);
    }

    [Fact]
    public async Task RemoveProfessionFromProvider_Returns200_ReturnsSuccess()
    {
        var sut = new ProfessionApiService(CreateFactory(HttpStatusCode.OK, """{"data":[],"errors":[]}"""));

        var result = await sut.RemoveProfessionFromProviderAsync("pat@test.dev", "Coaching");

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task RemoveProfessionFromProvider_Returns409_ReturnsGuardMessage()
    {
        var json = """{"data":null,"errors":["Cannot remove a profession while you have active appointments."]}""";
        var sut = new ProfessionApiService(CreateFactory(HttpStatusCode.Conflict, json));

        var result = await sut.RemoveProfessionFromProviderAsync("pat@test.dev", "Coaching");

        Assert.False(result.Success);
        Assert.Equal("Cannot remove a profession while you have active appointments.", result.ErrorMessage);
    }

    [Fact]
    public async Task RemoveProfessionFromProvider_Returns404_ReturnsNullMessage()
    {
        var sut = new ProfessionApiService(CreateFactory(HttpStatusCode.NotFound));

        var result = await sut.RemoveProfessionFromProviderAsync("pat@test.dev", "Coaching");

        Assert.False(result.Success);
        Assert.Null(result.ErrorMessage);
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
