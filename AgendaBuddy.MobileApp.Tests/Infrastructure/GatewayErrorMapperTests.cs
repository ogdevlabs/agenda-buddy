using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Resources.Strings;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

// ux-review.md finding 2 / PRD Requirement 12 / AC13: the gateway's failedService cluster id maps to
// a human-readable display name, never the raw cluster id, with a generic fallback for a network
// error that never reached the gateway or an unrecognized id.
[Collection(nameof(CultureSensitiveCollection))]
public class GatewayErrorMapperTests : IDisposable
{
    private readonly System.Globalization.CultureInfo? _originalCulture = AppResources.Culture;

    public GatewayErrorMapperTests() =>
        AppResources.Culture = new System.Globalization.CultureInfo("en");

    public void Dispose() => AppResources.Culture = _originalCulture;

    [Theory]
    [InlineData("booking", "Booking is unavailable right now. Try again.")]
    [InlineData("calendar", "Calendar is unavailable right now. Try again.")]
    [InlineData("customer", "Customers is unavailable right now. Try again.")]
    [InlineData("provider", "Providers is unavailable right now. Try again.")]
    [InlineData("services", "Services is unavailable right now. Try again.")]
    [InlineData("profession", "Professions is unavailable right now. Try again.")]
    [InlineData("identity", "Account is unavailable right now. Try again.")]
    public void Describe_KnownClusterId_ReturnsDisplayNameMessage(string failedService, string expected)
    {
        Assert.Equal(expected, GatewayErrorMapper.Describe(failedService));
    }

    [Fact]
    public void Describe_KnownClusterId_IsCaseInsensitive()
    {
        Assert.Equal("Booking is unavailable right now. Try again.", GatewayErrorMapper.Describe("BOOKING"));
    }

    [Fact]
    public void Describe_UnrecognizedClusterId_ReturnsGenericMessage()
    {
        Assert.Equal(GatewayErrorMapper.GenericMessage, GatewayErrorMapper.Describe("not-a-real-cluster"));
    }

    [Fact]
    public void Describe_Null_ReturnsGenericMessage()
    {
        Assert.Equal(GatewayErrorMapper.GenericMessage, GatewayErrorMapper.Describe(null));
    }

    [Fact]
    public void Describe_EmptyString_ReturnsGenericMessage()
    {
        Assert.Equal(GatewayErrorMapper.GenericMessage, GatewayErrorMapper.Describe(string.Empty));
    }
}
