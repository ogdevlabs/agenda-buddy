using MobileApp.Models;
using MobileApp.Services;
using MobileApp.ViewModels;
using Moq;
using Xunit;

namespace MobileApp.Tests.ViewModels;

public class CalendarViewModelTests
{
    [Fact]
    public async Task LoadAsync_Success_SetsDays()
    {
        var days = new List<CalendarDaySummary>
        {
            new() { Date = "2026-08-01", AvailableSlots = ["09:00", "10:00"], BookedSlots = ["11:00"] },
            new() { Date = "2026-08-02", AvailableSlots = [],                 BookedSlots = ["09:00"] }
        };

        var service = new Mock<ICalendarApiService>();
        service.Setup(s => s.GetAvailabilityAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(days);

        var vm = new CalendarViewModel(service.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Days.Count);
        Assert.Equal("2026-08-01", vm.Days[0].Date);
        Assert.False(vm.HasError);
        Assert.Empty(vm.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_NetworkError_SetsErrorMessage()
    {
        var service = new Mock<ICalendarApiService>();
        service.Setup(s => s.GetAvailabilityAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network unreachable"));

        var vm = new CalendarViewModel(service.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal("Could not load calendar — check your connection and try again.", vm.ErrorMessage);
    }
}
