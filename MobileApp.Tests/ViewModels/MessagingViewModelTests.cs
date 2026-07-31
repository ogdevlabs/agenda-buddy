using MobileApp.Models;
using MobileApp.Services;
using MobileApp.ViewModels;
using Moq;
using Xunit;

namespace MobileApp.Tests.ViewModels;

public class MessagingViewModelTests
{
    // ---------------------------------------------------------------------------
    // LoadAsync_Success_SetsThreads
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_Success_SetsThreads()
    {
        var threads = new List<MessageThreadStub>
        {
            new() { ThreadId = "t1", OtherPartyEmail = "alice@example.com", LastMessageBody = "Hello!", UnreadCount = 2 },
            new() { ThreadId = "t2", OtherPartyEmail = "bob@example.com",   LastMessageBody = "See you", UnreadCount = 0 }
        };

        var service = new Mock<IMessagingApiService>();
        service.Setup(s => s.GetInboxAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(threads);

        var vm = new MessagingViewModel(service.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Threads.Count);
        Assert.Equal("t1", vm.Threads[0].ThreadId);
        Assert.Equal("alice@example.com", vm.Threads[0].OtherPartyEmail);
        Assert.False(vm.HasError);
        Assert.Empty(vm.ErrorMessage);
    }

    // ---------------------------------------------------------------------------
    // LoadAsync_NetworkError_SetsErrorMessage
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_NetworkError_SetsErrorMessage()
    {
        var service = new Mock<IMessagingApiService>();
        service.Setup(s => s.GetInboxAsync(It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network unreachable"));

        var vm = new MessagingViewModel(service.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    // ---------------------------------------------------------------------------
    // LoadAsync_EmptyResult_IsEmptyIsTrue
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_EmptyResult_IsEmptyIsTrue()
    {
        var service = new Mock<IMessagingApiService>();
        service.Setup(s => s.GetInboxAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<MessageThreadStub>());

        var vm = new MessagingViewModel(service.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Threads);
        Assert.False(vm.HasError);
        Assert.False(vm.IsLoading);
        Assert.True(vm.IsEmpty);
    }
}
