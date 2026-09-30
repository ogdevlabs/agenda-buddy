using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class AuthenticatedImageSourceTests
{
    [Theory]
    [InlineData(null, "h")]
    [InlineData("r", null)]
    [InlineData(" ", "h")]
    [InlineData("r", "")]
    public void AMissingHalfNamesNoImage(string? providerRef, string? hash)
    {
        Assert.Null(AuthenticatedImageSource.For(providerRef, hash, MediaVariant.Thumb));
        Assert.False(AuthenticatedImageSource.PhotoSupersedesMark(providerRef, hash));
    }

    [Fact]
    public void BothHalvesNameTheImageTrimmed()
    {
        var source = AuthenticatedImageSource.For(" ref ", " hash ", MediaVariant.Full);

        Assert.Equal(new AuthenticatedImageSource("ref", "hash", MediaVariant.Full), source);
        Assert.True(AuthenticatedImageSource.PhotoSupersedesMark("ref", "hash"));
    }
}
