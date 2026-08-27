using Microsoft.Extensions.Configuration;
using AgendaBuddy.MobileApp.Infrastructure;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class ApiBaseUrlResolverTests
{
    // The client must resolve the gateway's address without a hardcoded, possibly
    // stale port, and it must prefer the value scripts/run-ios.sh injects over anything else.
    [Fact]
    public void Resolve_EnvironmentVariableSet_WinsOverConfigurationAndFallback()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["ApiBaseUrl"]).Returns("http://config-value/");

        var result = ApiBaseUrlResolver.Resolve(config.Object, _ => "http://gateway-from-env/");

        Assert.Equal("http://gateway-from-env/", result);
    }

    [Fact]
    public void Resolve_NoEnvironmentVariable_FallsBackToConfiguration()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["ApiBaseUrl"]).Returns("http://config-value/");

        var result = ApiBaseUrlResolver.Resolve(config.Object, _ => null);

        Assert.Equal("http://config-value/", result);
    }

    [Fact]
    public void Resolve_NoEnvironmentVariableAndNoConfiguration_FallsBackToHardcodedDefault()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["ApiBaseUrl"]).Returns((string?)null);

        var result = ApiBaseUrlResolver.Resolve(config.Object, _ => null);

        Assert.Equal(ApiBaseUrlResolver.DefaultBaseUrl, result);
    }
}
