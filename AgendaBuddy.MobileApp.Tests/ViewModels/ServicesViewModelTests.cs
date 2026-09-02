using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class ServicesViewModelTests
{
    private static IUserSessionService CreateSession(string email = "provider@example.com")
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(email);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session.Object;
    }

    private static IProfessionApiService CreateProfessionApi(params string[] professions)
    {
        var api = new Mock<IProfessionApiService>();
        api.Setup(a => a.GetProviderProfessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(professions.ToList());
        return api.Object;
    }

    [Fact]
    public async Task AddServiceAsync_ParsesDurationAndSendsIt()
    {
        var api = new Mock<IServicesApiService>();
        List<ServiceItem>? sent = null;
        api.Setup(a => a.AddServicesAsync("provider@example.com", It.IsAny<List<ServiceItem>>(), It.IsAny<CancellationToken>()))
           .Callback<string, List<ServiceItem>, CancellationToken>((_, items, _) => sent = items)
           .ReturnsAsync(true);
        api.Setup(a => a.GetServicesAsync("provider@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(new List<ServiceItem>());

        var vm = new ServicesViewModel(api.Object, CreateProfessionApi("Fitness"), CreateSession())
        {
            NewServiceName = "Consultation",
            NewServiceDescription = "30 min",
            NewServiceFee = "50",
            NewServiceDuration = "30",
            NewServiceProfessionName = "Fitness"
        };

        await vm.AddServiceCommand.ExecuteAsync(null);

        Assert.NotNull(sent);
        Assert.Equal(30, sent![0].DurationMinutes);
        Assert.Equal(50, sent[0].Fee);
        Assert.Empty(vm.NewServiceName);
    }

    [Fact]
    public async Task RemoveConfirmedAsync_Success_RemovesFromList()
    {
        var api = new Mock<IServicesApiService>();
        api.Setup(a => a.RemoveServiceAsync("provider@example.com", "Massage", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var vm = new ServicesViewModel(api.Object, CreateProfessionApi(), CreateSession())
        {
            Services = new List<ServiceItem>
            {
                new() { Name = "Massage" },
                new() { Name = "Consultation" }
            }
        };

        await vm.RemoveConfirmedAsync(vm.Services[0]);

        Assert.Single(vm.Services);
        Assert.Equal("Consultation", vm.Services[0].Name);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task RemoveConfirmedAsync_Failure_SetsErrorAndKeepsItem()
    {
        var api = new Mock<IServicesApiService>();
        api.Setup(a => a.RemoveServiceAsync("provider@example.com", "Massage", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var vm = new ServicesViewModel(api.Object, CreateProfessionApi(), CreateSession())
        {
            Services = new List<ServiceItem> { new() { Name = "Massage" } }
        };

        await vm.RemoveConfirmedAsync(vm.Services[0]);

        Assert.Single(vm.Services);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task SaveServiceAsync_SendsIsActiveAndDuration()
    {
        var api = new Mock<IServicesApiService>();
        List<ServiceItem>? sent = null;
        api.Setup(a => a.UpdateServicesAsync("provider@example.com", It.IsAny<List<ServiceItem>>(), It.IsAny<CancellationToken>()))
           .Callback<string, List<ServiceItem>, CancellationToken>((_, items, _) => sent = items)
           .ReturnsAsync(true);

        var vm = new ServicesViewModel(api.Object, CreateProfessionApi(), CreateSession());
        var item = new ServiceItem { Name = "Massage", IsActive = false, DurationMinutes = 45, IsEditing = true };

        await vm.SaveServiceCommand.ExecuteAsync(item);

        Assert.NotNull(sent);
        Assert.False(sent![0].IsActive);
        Assert.Equal(45, sent[0].DurationMinutes);
        Assert.False(item.IsEditing);
    }
}
