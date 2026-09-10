using AgendaBuddy.Library.Services;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

public class LocalEmailSenderTest
{
    [Fact]
    public async Task CapturesTheLatestMessagePerRecipient()
    {
        var sender = new LocalEmailSender();

        Assert.True(await sender.SendAsync(
            "customer@local.test",
            "Confirm",
            "Plain link",
            "<a href=\"agendame://email/confirm-email?token=abc\">Confirm</a>"));

        var message = sender.LatestFor("CUSTOMER@local.test");
        Assert.NotNull(message);
        Assert.Equal("Confirm", message.Subject);
        Assert.Contains("agendame://email/confirm-email", message.Html);
    }

    [Fact]
    public async Task ANewMessageReplacesThePreviousOneForThatRecipient()
    {
        var sender = new LocalEmailSender();
        await sender.SendAsync("customer@local.test", "First", "first");
        await sender.SendAsync("customer@local.test", "Second", "second");

        Assert.Equal("Second", sender.LatestFor("customer@local.test")?.Subject);
    }
}
