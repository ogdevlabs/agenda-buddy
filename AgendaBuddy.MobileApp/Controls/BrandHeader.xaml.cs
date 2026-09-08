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

    /// <summary>
    /// Whether this view was pushed onto something, and so has somewhere to go back to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Derived from the Shell navigation stack, never set per page.</b> Every view in this app hides the
    /// native navigation bar (<c>Shell.NavBarIsVisible="False"</c>), which also hides the platform's back
    /// button — so on a pushed page the only way out was the iOS edge-swipe gesture, and nothing on screen said
    /// so. Four pages had grown their own "← Back" button and the rest had nothing, which is exactly the drift a
    /// per-page affordance produces.
    /// </para>
    /// <para>
    /// Computing it here means a new view gets a back button by existing, and a tab root does not get a useless
    /// one: a tab's stack is one page deep, a pushed page's is two or more.
    /// </para>
    /// </remarks>
    public static readonly BindableProperty CanGoBackProperty =
        BindableProperty.Create(nameof(CanGoBack), typeof(bool), typeof(BrandHeader), defaultValue: false);

    public bool CanGoBack
    {
        get => (bool)GetValue(CanGoBackProperty);
        private set => SetValue(CanGoBackProperty, value);
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

        RefreshCanGoBack();
    }

    /// <summary>
    /// Re-reads the navigation depth when the hosting page appears.
    /// </summary>
    /// <remarks>
    /// <c>OnParentSet</c> alone is not enough: it fires while the page is being built, which on some paths is
    /// <b>before</b> Shell has pushed it onto the stack — so the depth read there is the depth of the page
    /// underneath. Recomputing on the appearing event is what makes the button correct on the first render
    /// rather than on the second visit.
    /// </remarks>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
            return;

        RefreshCanGoBack();

        // Shell raises this every time a page is shown, including on a pop back to a page that was already
        // built — where the depth has changed and OnParentSet will not fire again.
        if (Shell.Current is { } shell)
        {
            shell.Navigated -= OnShellNavigated;
            shell.Navigated += OnShellNavigated;
        }
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e) => RefreshCanGoBack();

    /// <summary>
    /// A tab root is one page deep; anything pushed on top of it is two or more.
    /// </summary>
    /// <remarks>
    /// Wrapped, because <c>Shell.Current</c> is null during teardown and while a modal is being dismissed, and a
    /// throw out of a header refresh would take down the page it decorates.
    /// </remarks>
    private void RefreshCanGoBack()
    {
        try
        {
            CanGoBack = Shell.Current?.Navigation?.NavigationStack?.Count > 1;
        }
        catch (Exception)
        {
            CanGoBack = false;
        }
    }

    /// <summary>
    /// Pops one level. Relative <c>".."</c> rather than a named route, so the header needs to know nothing about
    /// where it is.
    /// </summary>
    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception)
        {
            // Nothing to pop — the button should not have been visible, and a crash is a worse answer than a
            // tap that does nothing.
        }
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
