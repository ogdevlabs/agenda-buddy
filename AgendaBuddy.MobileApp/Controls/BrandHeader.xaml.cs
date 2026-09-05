#if MOBILE
using Microsoft.Extensions.DependencyInjection;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Controls;

/// <summary>
/// The app-identity band every view carries, placed directly under the native navigation bar, with the
/// signed-in user on its second line.
/// </summary>
/// <remarks>
/// This sits below the native bar rather than replacing its title, which means both are visible. That is a
/// deliberate product choice, not an oversight: the native bar cannot be suppressed in this MAUI version
/// (Shell.NavBarIsVisible, NavigationPage.HasNavigationBar and their code-behind setters are all
/// ineffective here), and a branded band was preferred over surrendering the header to the platform title.
/// </remarks>
public partial class BrandHeader : ContentView
{
    /// <summary>
    /// Whether to show the signed-in user under the brand. Off for a view that already puts the user
    /// somewhere better of its own — the dashboard names them in the greeting, so repeating it here would
    /// say the same thing twice in one screenful.
    /// </summary>
    public static readonly BindableProperty ShowUserProperty =
        BindableProperty.Create(nameof(ShowUser), typeof(bool), typeof(BrandHeader), defaultValue: true);

    public bool ShowUser
    {
        get => (bool)GetValue(ShowUserProperty);
        set => SetValue(ShowUserProperty, value);
    }

    public BrandHeader()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Binds to its own view model rather than inheriting the page's, and resolves it from the app's
    /// service provider rather than by injection, because XAML constructs this control itself — the page
    /// hosting it never sees it as a dependency.
    /// </summary>
    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent is null || BindingContext is BrandHeaderViewModel)
            return;

        var viewModel = IPlatformApplication.Current?.Services.GetService<BrandHeaderViewModel>();
        if (viewModel is null)
            return;

        BindingContext = viewModel;

        // RefreshAsync does not throw and is a no-op once the name is known for this account. It also brings
        // the unread badge up to date, which is why the count is current on every screen and not only on the
        // two that fetch notifications themselves.
        _ = viewModel.RefreshAsync();
    }

    /// <summary>
    /// The badge is the only notification affordance on most screens, so it has to be the way in as well as
    /// the signal. The route is registered globally, so this works from wherever the header is.
    /// </summary>
    private async void OnNotificationsTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("notifications");
    }
}
#endif
