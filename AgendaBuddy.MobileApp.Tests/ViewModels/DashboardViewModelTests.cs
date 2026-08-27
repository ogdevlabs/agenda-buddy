using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class DashboardViewModelTests
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
    public async Task LoadAsync_Success_SetsAppointmentsAndClearsError()
    {
        var appointments = new List<AppointmentSummary>
        {
            new() { Id = "a1", CustomerEmail = "alice@example.com", Status = AppointmentStatus.Requested }
        };

        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetTodayAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(appointments);

        var vm = new DashboardViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Single(vm.Appointments);
        Assert.Equal(string.Empty, vm.ErrorMessage);
        Assert.False(vm.HasError);
    }

    // A genuine failure surfaces the error banner (HasError + a real ErrorMessage),
    // never fabricated SeedDataProvider appointments.
    [Fact]
    public async Task LoadAsync_NetworkError_SetsHasErrorTrueWithRealMessage_NoFabricatedData()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetTodayAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network error"));

        var vm = new DashboardViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Appointments);
        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
        Assert.False(vm.IsEmpty);
    }

    // A genuine zero-result success surfaces the empty state (IsEmpty), never
    // fabricated SeedDataProvider appointments.
    [Fact]
    public async Task LoadAsync_EmptyResult_SetsIsEmptyTrue_NoFabricatedData()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetTodayAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<AppointmentSummary>());

        var vm = new DashboardViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Appointments);
        Assert.False(vm.HasError);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task LoadAsync_CustomerRole_SetsDisplayNameToProviderName()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetTodayAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<AppointmentSummary>());

        var vm = new DashboardViewModel(service.Object, CreateMockSession("alex.chen@agendabuddy.dev", "Customer").Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.All(vm.Appointments, a => Assert.False(string.IsNullOrEmpty(a.DisplayName)));
        Assert.All(vm.Appointments, a => Assert.NotEqual("Alex Chen", a.DisplayName));
    }

    [Fact]
    public async Task LoadAsync_ProviderRole_SetsDisplayNameToCustomerName()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetTodayAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<AppointmentSummary>());

        var vm = new DashboardViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.All(vm.Appointments, a => Assert.Equal(a.CustomerName, a.DisplayName));
    }
}
