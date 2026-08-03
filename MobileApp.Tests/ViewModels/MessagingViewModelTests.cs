using MobileApp.Models;
using MobileApp.Services;
using MobileApp.ViewModels;
using Moq;
using Xunit;

namespace MobileApp.Tests.ViewModels;

public class MessagingViewModelTests
{
    private static Mock<IUserSessionService> CreateMockSession(string email = "sarah.mitchell@agendabuddy.dev", string role = "Provider")
    {
        var session = new Mock<IUserSessionService>();
        session.Setup(s => s.Email).Returns(email);
        session.Setup(s => s.Role).Returns(role);
        session.Setup(s => s.IsProvider).Returns(role == "Provider");
        session.Setup(s => s.IsCustomer).Returns(role == "Customer");
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session;
    }

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

        var vm = new MessagingViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Threads.Count);
        Assert.Equal("t1", vm.Threads[0].ThreadId);
        Assert.Equal("alice@example.com", vm.Threads[0].OtherPartyEmail);
        Assert.False(vm.HasError);
        Assert.Empty(vm.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_NetworkError_FallsBackToSeedData()
    {
        var service = new Mock<IMessagingApiService>();
        service.Setup(s => s.GetInboxAsync(It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network unreachable"));

        var vm = new MessagingViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.Threads);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task LoadAsync_EmptyResult_FallsBackToSeedData()
    {
        var service = new Mock<IMessagingApiService>();
        service.Setup(s => s.GetInboxAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<MessageThreadStub>());

        var vm = new MessagingViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.Threads);
        Assert.False(vm.HasError);
        Assert.False(vm.IsLoading);
        Assert.False(vm.IsEmpty);
    }
}
