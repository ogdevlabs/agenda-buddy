using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class MessageThreadViewModelTests
{
    // ---------------------------------------------------------------------------
    // SendAsync_ValidBody_CallsServiceAndAppendsMessage
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_ValidBody_CallsServiceAndAppendsMessage()
    {
        var existingMessage = new MessageSummary
        {
            Id = "m0", ThreadId = "t1", SenderEmail = "alice@example.com",
            Body = "Hello!", SentAt = DateTime.UtcNow.AddMinutes(-5), IsRead = true
        };

        var newMessage = new MessageSummary
        {
            Id = "m1", ThreadId = "t1", SenderEmail = "provider@example.com",
            Body = "Hi there!", SentAt = DateTime.UtcNow, IsRead = false
        };

        var service = new Mock<IMessagingApiService>();
        // The real route keys on the counterpart's EMAIL, not ThreadId (MessagingRouteBuilder.Thread's
        // remarks) — LoadThreadAsync calls GetThreadAsync(RecipientEmail), not GetThreadAsync(ThreadId).
        service.Setup(s => s.GetThreadAsync("alice@example.com", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<MessageSummary> { existingMessage });
        service.Setup(s => s.SendMessageAsync("alice@example.com", "Hi there!", It.IsAny<CancellationToken>()))
               .ReturnsAsync(MessageSendResult.Sent(newMessage));

        var vm = new MessageThreadViewModel(service.Object)
        {
            ThreadId = "t1",
            RecipientEmail = "alice@example.com",
            NewMessageBody = "Hi there!"
        };

        // Load existing messages first
        await vm.LoadThreadCommand.ExecuteAsync(null);

        Assert.Single(vm.Messages);

        // Send
        await vm.SendCommand.ExecuteAsync(null);

        service.Verify(s => s.SendMessageAsync("alice@example.com", "Hi there!", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, vm.Messages.Count);
        Assert.Equal("m1", vm.Messages[1].Id);
        Assert.Equal("Hi there!", vm.Messages[1].Body);
        Assert.Empty(vm.NewMessageBody);
        Assert.Empty(vm.ErrorMessage);
        // The appended bubble is the caller's own. Without this it drew right-aligned as the counterpart's,
        // because IsMine is set by LoadThreadAsync and a locally appended message never went through it.
        Assert.True(vm.Messages[1].IsMine);
    }

    // ---------------------------------------------------------------------------
    // A refusal is not a connection problem
    // ---------------------------------------------------------------------------

    // THE reported symptom: sending to somebody the caller has no subscription with answers 403, and the app
    // told them to check a connection that was working. The body is put back so the refusal does not also lose
    // what they typed.
    [Fact]
    public async Task SendAsync_Refused_ReportsTheRuleAndKeepsTheBody()
    {
        var service = new Mock<IMessagingApiService>();
        service.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(MessageSendResult.Refused(System.Net.HttpStatusCode.Forbidden));

        var vm = new MessageThreadViewModel(service.Object)
        {
            RecipientEmail = "stranger@example.com",
            NewMessageBody = "Hi there!"
        };

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal(MessageSendResult.NotPermittedMessage, vm.ErrorMessage);
        Assert.DoesNotContain("connection", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Hi there!", vm.NewMessageBody);
        Assert.Empty(vm.Messages);
    }

    [Fact]
    public async Task SendAsync_Unreachable_ReportsTheConnection()
    {
        var service = new Mock<IMessagingApiService>();
        service.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(MessageSendResult.Unreachable());

        var vm = new MessageThreadViewModel(service.Object)
        {
            RecipientEmail = "alice@example.com",
            NewMessageBody = "Hi there!"
        };

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Equal(MessageSendResult.UnreachableMessage, vm.ErrorMessage);
        Assert.Equal("Hi there!", vm.NewMessageBody);
    }

    // A 201 whose body did not bind is a stored message. Nothing is appended, and nothing is reported —
    // reporting a failure here is what would produce a duplicate send.
    [Fact]
    public async Task SendAsync_SucceededWithNoBody_ClearsTheComposerAndSaysNothing()
    {
        var service = new Mock<IMessagingApiService>();
        service.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(MessageSendResult.Sent(null));

        var vm = new MessageThreadViewModel(service.Object)
        {
            RecipientEmail = "alice@example.com",
            NewMessageBody = "Hi there!"
        };

        await vm.SendCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage);
        Assert.Empty(vm.NewMessageBody);
        Assert.Empty(vm.Messages);
    }

    // ---------------------------------------------------------------------------
    // CanSend_EmptyBody_ReturnsFalse
    // ---------------------------------------------------------------------------

    [Fact]
    public void CanSend_EmptyBody_ReturnsFalse()
    {
        var service = new Mock<IMessagingApiService>();
        var vm = new MessageThreadViewModel(service.Object)
        {
            NewMessageBody = string.Empty
        };

        Assert.False(vm.SendCommand.CanExecute(null));
    }

    // ---------------------------------------------------------------------------
    // CanSend_WhitespaceBody_ReturnsFalse
    // ---------------------------------------------------------------------------

    [Fact]
    public void CanSend_WhitespaceBody_ReturnsFalse()
    {
        var service = new Mock<IMessagingApiService>();
        var vm = new MessageThreadViewModel(service.Object)
        {
            NewMessageBody = "   "
        };

        Assert.False(vm.SendCommand.CanExecute(null));
    }

    // ---------------------------------------------------------------------------
    // CanSend_PopulatedBody_ReturnsTrue
    // ---------------------------------------------------------------------------

    [Fact]
    public void CanSend_PopulatedBody_ReturnsTrue()
    {
        var service = new Mock<IMessagingApiService>();
        var vm = new MessageThreadViewModel(service.Object)
        {
            NewMessageBody = "Hello!"
        };

        Assert.True(vm.SendCommand.CanExecute(null));
    }
}
