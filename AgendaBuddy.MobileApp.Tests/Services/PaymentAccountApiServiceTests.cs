using System.Net;
using System.Text;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

public class PaymentAccountApiServiceTests
{
    [Fact]
    public async Task BeginCustomerSetup_PaymentSetupUnavailable_ThrowsDedicatedException()
    {
        var sut = new PaymentAccountApiService(CreateFactory(
            HttpStatusCode.ServiceUnavailable,
            """{"title":"payment_setup_unavailable"}"""));

        await Assert.ThrowsAsync<PaymentSetupUnavailableException>(() => sut.BeginCustomerSetupAsync());
    }

    [Fact]
    public async Task BeginCustomerSetup_UnrelatedServiceUnavailable_ReturnsNull()
    {
        var sut = new PaymentAccountApiService(CreateFactory(
            HttpStatusCode.ServiceUnavailable,
            """{"title":"gateway-destination-unreachable"}"""));

        var result = await sut.BeginCustomerSetupAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task BeginCustomerSetup_EmptyServiceUnavailable_ReturnsNull()
    {
        var sut = new PaymentAccountApiService(CreateFactory(HttpStatusCode.ServiceUnavailable, string.Empty));

        var result = await sut.BeginCustomerSetupAsync();

        Assert.Null(result);
    }

    private static IHttpClientFactory CreateFactory(HttpStatusCode statusCode, string json)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var client = new HttpClient(new FakeHttpMessageHandler(statusCode, content))
        {
            BaseAddress = new Uri("https://localhost/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("AgendaBuddyApi")).Returns(client);
        return factory.Object;
    }

    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, HttpContent content)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode) { Content = content });
    }
}
