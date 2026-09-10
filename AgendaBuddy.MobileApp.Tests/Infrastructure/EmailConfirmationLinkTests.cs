using AgendaBuddy.MobileApp.Infrastructure;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class EmailConfirmationLinkTests
{
    [Fact]
    public void AgendaMeHttpsLinkReturnsDecodedToken()
    {
        var result = EmailConfirmationLink.Parse(
            new Uri("https://agendame.app/confirm-email?token=a%2Bb%2Fc%3D"));

        Assert.True(result.IsValid);
        Assert.Equal("a+b/c=", result.Token);
    }

    [Theory]
    [InlineData("http://agendame.app/confirm-email?token=abc")]
    [InlineData("https://evil.example/confirm-email?token=abc")]
    [InlineData("https://agendame.app/other?token=abc")]
    [InlineData("https://agendame.app/confirm-email")]
    public void UntrustedOrIncompleteLinkIsRejected(string value)
    {
        Assert.False(EmailConfirmationLink.Parse(new Uri(value)).IsValid);
    }
}