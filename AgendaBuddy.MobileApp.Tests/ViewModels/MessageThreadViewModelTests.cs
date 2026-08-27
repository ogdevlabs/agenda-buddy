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
        service.Setup(s => s.GetThreadAsync("t1", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<MessageSummary> { existingMessage });
        service.Setup(s => s.SendMessageAsync("alice@example.com", "Hi there!", It.IsAny<CancellationToken>()))
               .ReturnsAsync(newMessage);

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
