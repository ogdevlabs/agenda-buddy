using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class CalendarViewModelTests
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

        var vm = new CalendarViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Days.Count);
        Assert.Equal("2026-08-01", vm.Days[0].Date);
        Assert.False(vm.HasError);
        Assert.Empty(vm.ErrorMessage);
    }

    // A genuine failure surfaces the error banner (HasError + a real ErrorMessage),
    // never fabricated SeedDataProvider calendar days.
    [Fact]
    public async Task LoadAsync_NetworkError_SetsHasErrorTrueWithRealMessage_NoFabricatedData()
    {
        var service = new Mock<ICalendarApiService>();
        service.Setup(s => s.GetAvailabilityAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network unreachable"));

        var vm = new CalendarViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Days);
        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
    }

    // A genuine zero-result success is assigned as-is, never replaced with
    // fabricated SeedDataProvider calendar days.
    [Fact]
    public async Task LoadAsync_EmptyResult_NoFabricatedData()
    {
        var service = new Mock<ICalendarApiService>();
        service.Setup(s => s.GetAvailabilityAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<CalendarDaySummary>());

        var vm = new CalendarViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Days);
        Assert.False(vm.HasError);
    }
}
