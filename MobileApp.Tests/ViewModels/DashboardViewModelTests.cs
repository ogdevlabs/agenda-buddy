using Library.Entities;
using MobileApp.Models;
using MobileApp.Services;
using MobileApp.ViewModels;
using Moq;
using Xunit;

namespace MobileApp.Tests.ViewModels;

public class DashboardViewModelTests
{
    // ---------------------------------------------------------------------------
    // LoadAsync — success path
    // ---------------------------------------------------------------------------

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

        var vm = new DashboardViewModel(service.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Single(vm.Appointments);
        Assert.Equal(string.Empty, vm.ErrorMessage);
        Assert.False(vm.HasError);
    }

    // ---------------------------------------------------------------------------
    // LoadAsync — network error
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_NetworkError_SetsErrorMessage()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetTodayAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network error"));

        var vm = new DashboardViewModel(service.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal(
            "Could not load appointments — check your connection and try again.",
            vm.ErrorMessage);
    }

    // ---------------------------------------------------------------------------
    // LoadAsync — empty result
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_EmptyResult_IsEmptyIsTrue()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetTodayAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<AppointmentSummary>());

        var vm = new DashboardViewModel(service.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Appointments);
        Assert.False(vm.HasError);
        Assert.True(vm.IsEmpty);
    }
}
