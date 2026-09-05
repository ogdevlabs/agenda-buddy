using AgendaBuddy.Library;
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

    // The assertion above compares the fallback to itself, so it held even while the fallback named
    // 6036 — Identity's standalone port, which nothing listens on under the AppHost. Every request from
    // an app launched without MAUI_API_BASE_URL went into a void and surfaced as a login failure. This
    // pins the property that actually matters: the fallback is the Gateway's own reserved address.
    [Fact]
    public void DefaultBaseUrl_IsTheGatewaysPinnedLocalAddress_NotSomeOtherServicesPort()
    {
        Assert.Equal(LocalGatewayAddress.BaseUrl, ApiBaseUrlResolver.DefaultBaseUrl);
        Assert.Equal($"http://localhost:{LocalGatewayAddress.Port}/", ApiBaseUrlResolver.DefaultBaseUrl);
    }

    // A relative path is appended to this, so a missing trailing slash silently drops the last segment
    // of the base path when combined into a Uri.
    [Fact]
    public void DefaultBaseUrl_EndsWithATrailingSlash()
    {
        Assert.EndsWith("/", ApiBaseUrlResolver.DefaultBaseUrl);
    }

    // ── Android emulator loopback remap ─────────────────────────────────────────────────────────────
    // The emulator is a separate device: localhost inside it is the emulator, not the host machine. Applied
    // on Android only, which is why it is a separate method rather than part of Resolve.

    [Theory]
    [InlineData("http://localhost:6080/", "http://10.0.2.2:6080/")]
    [InlineData("http://localhost:6080", "http://10.0.2.2:6080/")]
    [InlineData("http://127.0.0.1:6080/", "http://10.0.2.2:6080/")]
    [InlineData("http://LOCALHOST:6080/", "http://10.0.2.2:6080/")]
    public void ALoopbackAddressIsRemappedToTheEmulatorHostAlias(string input, string expected)
    {
        Assert.Equal(expected, ApiBaseUrlResolver.RemapLoopbackForAndroidEmulator(input));
    }

    /// <summary>
    /// The important half: a real host must be returned untouched. A rewrite that fired on a deployed address
    /// would silently point a production build at a private IP.
    /// </summary>
    [Theory]
    [InlineData("https://gateway.greenwater-94f070ec.eastus.azurecontainerapps.io")]
    [InlineData("https://api.agendame.app/")]
    [InlineData("http://192.168.1.50:6080/")]
    public void ANonLoopbackAddressIsLeftAlone(string input)
    {
        Assert.Equal(input, ApiBaseUrlResolver.RemapLoopbackForAndroidEmulator(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    public void AnUnusableValueIsReturnedUnchangedRatherThanThrowing(string input)
    {
        Assert.Equal(input, ApiBaseUrlResolver.RemapLoopbackForAndroidEmulator(input));
    }

    // The pinned local gateway address is the fallback, so it must be exactly what the remap handles -- if the
    // two ever disagree, an Android debug run silently dials the emulator itself.
    [Fact]
    public void TheDefaultFallbackIsRemappable()
    {
        var remapped = ApiBaseUrlResolver.RemapLoopbackForAndroidEmulator(ApiBaseUrlResolver.DefaultBaseUrl);

        Assert.Contains("10.0.2.2", remapped);
        Assert.DoesNotContain("localhost", remapped);
    }
}
