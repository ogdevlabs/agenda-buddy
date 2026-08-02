#if MOBILE
using Library.Entities;
using MobileApp.Infrastructure;
using MobileApp.Models;
using MobileApp.ViewModels;

namespace MobileApp.Views;

[QueryProperty(nameof(AppointmentId), "appointmentId")]
[QueryProperty(nameof(CustomerEmail), "customerEmail")]
[QueryProperty(nameof(CustomerName), "customerName")]
[QueryProperty(nameof(CustomerPhone), "customerPhone")]
[QueryProperty(nameof(ScheduledAtStr), "scheduledAt")]
[QueryProperty(nameof(StatusStr), "status")]
[QueryProperty(nameof(ServiceName), "serviceName")]
[QueryProperty(nameof(CustomerNotes), "customerNotes")]
public partial class AppointmentDetailPage : ContentPage
{
    private readonly AppointmentDetailViewModel _viewModel;

    public string AppointmentId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ScheduledAtStr { get; set; } = string.Empty;
    public string StatusStr { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string CustomerNotes { get; set; } = string.Empty;

    public AppointmentDetailPage(AppointmentDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.ActionRequested += OnActionRequested;
        JwtDelegatingHandler.UnauthorizedAccess += OnUnauthorizedAccess;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.AppointmentId = AppointmentId;

        var fallback = new AppointmentDetail
        {
            Id = AppointmentId,
            CustomerEmail = CustomerEmail,
            CustomerName = CustomerName,
            CustomerPhone = CustomerPhone,
            ScheduledAt = DateTime.TryParse(ScheduledAtStr, out var dt) ? dt : DateTime.Now,
            Status = Enum.TryParse<AppointmentStatus>(StatusStr, out var st) ? st : AppointmentStatus.Requested,
            ServiceName = ServiceName,
            CustomerNotes = CustomerNotes
        };

        _viewModel.LoadWithFallbackCommand.Execute(fallback);
    }

    private async void OnActionRequested(object? sender, AppointmentActionEventArgs e)
    {
        switch (e.Action)
        {
            case ActionType.Confirm:
                await _viewModel.ExecuteStatusUpdateAsync(AppointmentStatus.Confirmed);
                break;

            case ActionType.Cancel:
                var cancelChoice = await DisplayActionSheetAsync(
                    "Cancel this appointment?",
                    "Keep it",
                    null,
                    "Cancel appointment");
                if (cancelChoice == "Cancel appointment")
                    await _viewModel.ExecuteStatusUpdateAsync(AppointmentStatus.Cancelled);
                break;

            case ActionType.Complete:
                var completeChoice = await DisplayActionSheetAsync(
                    "Mark this appointment as complete?",
                    "Go back",
                    null,
                    "Mark complete");
                if (completeChoice == "Mark complete")
                    await _viewModel.ExecuteStatusUpdateAsync(AppointmentStatus.Completed);
                break;
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnUnauthorizedAccess(object? sender, EventArgs e)
    {
        _viewModel.ErrorMessage = "Your session expired. Any unsaved changes were not saved.";
    }
}
#endif
