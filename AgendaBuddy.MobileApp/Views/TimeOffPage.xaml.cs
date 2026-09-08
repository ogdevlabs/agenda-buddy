#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

/// <summary>
/// A provider's time off. Reached from Calendar Settings, which is itself behind the calendar's gear.
/// </summary>
public partial class TimeOffPage : ContentPage
{
    private readonly TimeOffViewModel _viewModel;

    public TimeOffPage(TimeOffViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

}
#endif
