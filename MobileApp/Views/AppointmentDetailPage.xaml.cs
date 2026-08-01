#if MOBILE
using Library.Entities;
using MobileApp.Infrastructure;
using MobileApp.ViewModels;

namespace MobileApp.Views;

[QueryProperty(nameof(AppointmentId), "appointmentId")]
public partial class AppointmentDetailPage : ContentPage
{
    private readonly AppointmentDetailViewModel _viewModel;

    public string AppointmentId
    {
        set => _viewModel.AppointmentId = value;
    }

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
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnActionRequested(object? sender, AppointmentActionEventArgs e)
    {
        switch (e.Action)
        {
            case ActionType.Confirm:
                // UX F-005: confirming an appointment is affirmative — no bottom sheet needed.
                await _viewModel.ExecuteStatusUpdateAsync(AppointmentStatus.Confirmed);
                break;

            case ActionType.Cancel:
                // UX F-005: destructive action → bottom sheet (DisplayActionSheet), NOT modal alert.
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

    private void OnUnauthorizedAccess(object? sender, EventArgs e)
    {
        _viewModel.ErrorMessage = "Your session expired. Any unsaved changes were not saved.";
    }
}
#endif
