using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// A provider sets their own calendar day. The end hour is exclusive, so the pickers and the saved payload
/// have to agree on what "day ends at 17:00" means, and a window that opens at or after it closes has to be
/// refused rather than quietly corrected.
/// </summary>
public class CalendarSettingsViewModelTests
{
    private const string Email = "coach@example.com";

    private static Mock<IUserSessionService> Session(bool isProvider = true)
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(Email);
        session.SetupGet(s => s.Role).Returns(isProvider ? "provider" : "customer");
        session.SetupGet(s => s.IsProvider).Returns(isProvider);
        session.SetupGet(s => s.IsCustomer).Returns(!isProvider);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session;
    }

    private static CalendarSettingsViewModel Build(
        Mock<IProviderApiService> providerApi, bool isProvider = true) =>
        new(providerApi.Object, Session(isProvider).Object);

    private static Mock<IProviderApiService> Api(WorkHours? stored)
    {
        var api = new Mock<IProviderApiService>();
        api.Setup(p => p.GetWorkHoursAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(stored);
        api.Setup(p => p.UpdateWorkHoursAsync(It.IsAny<string>(), It.IsAny<WorkHours>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(true);
        return api;
    }

    [Fact]
    public void BeforeAnythingLoadsTheWindowIsTheStandardEightToFive()
    {
        var vm = Build(Api(null));

        Assert.Equal(8, vm.StartHour);
        Assert.Equal(17, vm.EndHour);
    }

    [Fact]
    public void ThePickersCoverEveryHourAStartAndAnEndCanTake()
    {
        var vm = Build(Api(null));

        // A day cannot start at 24:00, and cannot end at 00:00.
        Assert.Equal(24, vm.StartHourOptions.Count);
        Assert.Equal("00:00", vm.StartHourOptions[0]);
        Assert.Equal("23:00", vm.StartHourOptions[23]);

        Assert.Equal(24, vm.EndHourOptions.Count);
        Assert.Equal("01:00", vm.EndHourOptions[0]);
        Assert.Equal("24:00", vm.EndHourOptions[23]);
    }

    [Fact]
    public async Task LoadingShowsTheStoredWindow()
    {
        var vm = Build(Api(new WorkHours(6, 14)));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(6, vm.StartHour);
        Assert.Equal(14, vm.EndHour);
        Assert.Equal(6, vm.StartHourIndex);
        // The end options begin at 01:00, so the index trails the hour by one.
        Assert.Equal(13, vm.EndHourIndex);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task AProviderWhoCannotBeReadSeesAnErrorRatherThanInventedHours()
    {
        var vm = Build(Api(null));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task AThrowingReadSurfacesAnErrorRatherThanPropagating()
    {
        var api = new Mock<IProviderApiService>();
        api.Setup(p => p.GetWorkHoursAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new HttpRequestException("gateway down"));

        var vm = Build(api);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task SavingSendsTheHoursThePickersShow()
    {
        var api = Api(new WorkHours(8, 17));
        WorkHours? sent = null;
        api.Setup(p => p.UpdateWorkHoursAsync(Email, It.IsAny<WorkHours>(), It.IsAny<CancellationToken>()))
           .Callback<string, WorkHours, CancellationToken>((_, hours, _) => sent = hours)
           .ReturnsAsync(true);

        var vm = Build(api);
        vm.StartHourIndex = 10;   // 10:00
        vm.EndHourIndex = 18;     // 19:00

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new WorkHours(10, 19), sent);
    }

    [Fact]
    public async Task SavingRaisesSavedSoThePageCanReturnToTheCalendar()
    {
        var vm = Build(Api(new WorkHours(8, 17)));
        var raised = 0;
        vm.Saved += (_, _) => raised++;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task ARejectedSaveKeepsThePageOpenAndSaysSo()
    {
        var api = Api(new WorkHours(8, 17));
        api.Setup(p => p.UpdateWorkHoursAsync(It.IsAny<string>(), It.IsAny<WorkHours>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(false);

        var vm = Build(api);
        var raised = 0;
        vm.Saved += (_, _) => raised++;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, raised);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task AnUnreachableServerKeepsThePageOpenToo()
    {
        var api = Api(new WorkHours(8, 17));
        api.Setup(p => p.UpdateWorkHoursAsync(It.IsAny<string>(), It.IsAny<WorkHours>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new HttpRequestException("gateway down"));

        var vm = Build(api);
        var raised = 0;
        vm.Saved += (_, _) => raised++;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, raised);
        Assert.True(vm.HasError);
    }

    [Theory]
    [InlineData(17, 7)]   // start 17:00, end 08:00
    [InlineData(9, 8)]    // start 09:00, end 09:00
    [InlineData(23, 0)]   // start 23:00, end 01:00
    public void AWindowThatDoesNotOpenBeforeItClosesCannotBeSaved(int startIndex, int endIndex)
    {
        var vm = Build(Api(new WorkHours(8, 17)));
        vm.StartHourIndex = startIndex;
        vm.EndHourIndex = endIndex;

        Assert.False(vm.IsWindowValid);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Contains("start before it ends", vm.WindowSummary);
    }

    [Fact]
    public void AValidWindowCanBeSavedAndIsDescribedBackToTheProvider()
    {
        var vm = Build(Api(new WorkHours(8, 17)));
        vm.StartHourIndex = 9;    // 09:00
        vm.EndHourIndex = 17;     // 18:00

        Assert.True(vm.IsWindowValid);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Equal("Bookable 09:00 to 18:00, 9 hours a day.", vm.WindowSummary);
    }

    [Fact]
    public void AWindowEndingAtMidnightIsValid()
    {
        var vm = Build(Api(new WorkHours(8, 17)));
        vm.StartHourIndex = 20;
        vm.EndHourIndex = 23;     // 24:00

        Assert.Equal(24, vm.EndHour);
        Assert.True(vm.IsWindowValid);
        Assert.Equal("Bookable 20:00 to 24:00, 4 hours a day.", vm.WindowSummary);
    }

    [Fact]
    public async Task OnlyAProviderHasACalendarToConfigure()
    {
        var vm = Build(Api(new WorkHours(8, 17)), isProvider: false);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.IsProvider);
    }
}
