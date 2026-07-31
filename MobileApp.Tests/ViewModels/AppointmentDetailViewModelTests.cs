using Library.Entities;
using MobileApp.Models;
using MobileApp.Services;
using MobileApp.ViewModels;
using Moq;
using Xunit;

namespace MobileApp.Tests.ViewModels;

public class AppointmentDetailViewModelTests
{
    private static AppointmentDetail Appt(string id = "a1", AppointmentStatus status = AppointmentStatus.Requested) => new()
    {
        Id = id,
        CustomerEmail = "alice@example.com",
        ProviderEmail = "prov@example.com",
        ScheduledAt = new DateTime(2026, 7, 31, 9, 0, 0, DateTimeKind.Utc),
        Status = status,
        ServiceId = "s1"
    };

    // ---------------------------------------------------------------------------
    // LoadAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_Success_SetsAppointment()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetAppointmentAsync("a1", It.IsAny<CancellationToken>()))
               .ReturnsAsync(Appt());

        var vm = new AppointmentDetailViewModel(service.Object) { AppointmentId = "a1" };

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotNull(vm.Appointment);
        Assert.Equal("a1", vm.Appointment!.Id);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task LoadAsync_NetworkError_SetsErrorMessage()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetAppointmentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("boom"));

        var vm = new AppointmentDetailViewModel(service.Object) { AppointmentId = "a1" };

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    // ---------------------------------------------------------------------------
    // Confirm / Cancel / Complete — success flow via ExecuteStatusUpdateAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConfirmAsync_Success_UpdatesAppointmentStatus()
    {
        var confirmed = Appt(status: AppointmentStatus.Confirmed);
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.UpdateStatusAsync("a1", AppointmentStatus.Confirmed, It.IsAny<CancellationToken>()))
               .ReturnsAsync(confirmed);

        var vm = new AppointmentDetailViewModel(service.Object) { AppointmentId = "a1" };

        await vm.ExecuteStatusUpdateAsync(AppointmentStatus.Confirmed);

        Assert.NotNull(vm.Appointment);
        Assert.Equal(AppointmentStatus.Confirmed, vm.Appointment!.Status);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task CancelAsync_Success_UpdatesAppointmentStatus()
    {
        var cancelled = Appt(status: AppointmentStatus.Cancelled);
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.UpdateStatusAsync("a1", AppointmentStatus.Cancelled, It.IsAny<CancellationToken>()))
               .ReturnsAsync(cancelled);

        var vm = new AppointmentDetailViewModel(service.Object) { AppointmentId = "a1" };

        await vm.ExecuteStatusUpdateAsync(AppointmentStatus.Cancelled);

        Assert.NotNull(vm.Appointment);
        Assert.Equal(AppointmentStatus.Cancelled, vm.Appointment!.Status);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task CompleteAsync_Success_UpdatesAppointmentStatus()
    {
        var completed = Appt(status: AppointmentStatus.Completed);
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.UpdateStatusAsync("a1", AppointmentStatus.Completed, It.IsAny<CancellationToken>()))
               .ReturnsAsync(completed);

        var vm = new AppointmentDetailViewModel(service.Object) { AppointmentId = "a1" };

        await vm.ExecuteStatusUpdateAsync(AppointmentStatus.Completed);

        Assert.NotNull(vm.Appointment);
        Assert.Equal(AppointmentStatus.Completed, vm.Appointment!.Status);
        Assert.False(vm.HasError);
    }

    // ---------------------------------------------------------------------------
    // T-003: API returns 400 → service returns null → ErrorMessage set, no crash
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateStatusAsync_Returns400_SetsErrorMessage()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.UpdateStatusAsync(It.IsAny<string>(), It.IsAny<AppointmentStatus>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((AppointmentDetail?)null);

        var vm = new AppointmentDetailViewModel(service.Object) { AppointmentId = "a1" };

        await vm.ExecuteStatusUpdateAsync(AppointmentStatus.Confirmed);

        Assert.True(vm.HasError);
        Assert.Equal("Status update failed", vm.ErrorMessage);
    }

    // ---------------------------------------------------------------------------
    // Command wiring: Confirm/Cancel/Complete commands raise ActionRequested
    // ---------------------------------------------------------------------------

    [Fact]
    public void ConfirmCommand_RaisesActionRequestedWithConfirm()
    {
        var vm = new AppointmentDetailViewModel(new Mock<IBookingApiService>().Object);
        ActionType? captured = null;
        vm.ActionRequested += (_, e) => captured = e.Action;

        vm.ConfirmCommand.Execute(null);

        Assert.Equal(ActionType.Confirm, captured);
    }

    [Fact]
    public void CancelCommand_RaisesActionRequestedWithCancel()
    {
        var vm = new AppointmentDetailViewModel(new Mock<IBookingApiService>().Object);
        ActionType? captured = null;
        vm.ActionRequested += (_, e) => captured = e.Action;

        vm.CancelCommand.Execute(null);

        Assert.Equal(ActionType.Cancel, captured);
    }

    [Fact]
    public void CompleteCommand_RaisesActionRequestedWithComplete()
    {
        var vm = new AppointmentDetailViewModel(new Mock<IBookingApiService>().Object);
        ActionType? captured = null;
        vm.ActionRequested += (_, e) => captured = e.Action;

        vm.CompleteCommand.Execute(null);

        Assert.Equal(ActionType.Complete, captured);
    }
}
