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

        // RefreshAsync does not throw and is a no-op once the name is known for this account.
        _ = viewModel.RefreshAsync();
    }
}
#endif
