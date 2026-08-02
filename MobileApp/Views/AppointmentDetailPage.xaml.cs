#if MOBILE
using Library.Entities;
using MobileApp.Infrastructure;
using MobileApp.Models;
using MobileApp.ViewModels;

namespace MobileApp.Views;

[QueryProperty(nameof(AppointmentId), "appointmentId")]
[QueryProperty(nameof(CustomerEmail), "customerEmail")]
[QueryProperty(nameof(ProviderEmail), "providerEmail")]
[QueryProperty(nameof(ScheduledAtStr), "scheduledAt")]
[QueryProperty(nameof(StatusStr), "status")]
[QueryProperty(nameof(ServiceId), "serviceId")]
public partial class AppointmentDetailPage : ContentPage
{
    private readonly AppointmentDetailViewModel _viewModel;

    public string AppointmentId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string ProviderEmail { get; set; } = string.Empty;
    public string ScheduledAtStr { get; set; } = string.Empty;
    public string StatusStr { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;

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
            ProviderEmail = ProviderEmail,
            ScheduledAt = DateTime.TryParse(ScheduledAtStr, out var dt) ? dt : DateTime.Now,
            Status = Enum.TryParse<AppointmentStatus>(StatusStr, out var st) ? st : AppointmentStatus.Requested,
            ServiceId = ServiceId
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
