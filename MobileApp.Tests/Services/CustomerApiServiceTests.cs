using System.Net;
using System.Net.Http;
using System.Text;
using MobileApp.Services;
using Moq;
using Xunit;

namespace MobileApp.Tests.Services;

public class CustomerApiServiceTests
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
    public async Task GetCustomers_Returns200_DeserializesList()
    {
        const string json = """
            [
                {"id":"1","email":"alice@example.com","fullName":"Alice Smith"},
                {"id":"2","email":"bob@example.com","fullName":"Bob Jones"}
            ]
            """;

        var factory = CreateFactory(HttpStatusCode.OK, json);
        var sut = new CustomerApiService(factory);

        var result = await sut.GetCustomersAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Alice Smith", result[0].FullName);
        Assert.Equal("alice@example.com", result[0].Email);
        Assert.Equal("Bob Jones", result[1].FullName);
        Assert.Equal("bob@example.com", result[1].Email);
    }

    [Fact]
    public async Task GetCustomers_Returns401_ReturnsEmptyList()
    {
        var factory = CreateFactory(HttpStatusCode.Unauthorized);
        var sut = new CustomerApiService(factory);

        var result = await sut.GetCustomersAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // -----------------------------------------------------------------------
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
