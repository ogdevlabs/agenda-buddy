#if MOBILE
namespace AgendaBuddy.MobileApp.Controls;

/// <summary>
/// The app-identity band every view carries, placed directly under the native navigation bar.
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
}
#endif
