using Foundation;
using UIKit;

namespace AgendaBuddy.MobileApp;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
    {
#if DEBUG
    if (Uri.TryCreate(url.AbsoluteString, UriKind.Absolute, out var receivedUri))
        Console.WriteLine($"APP LINK: iOS OpenUrl {receivedUri.Scheme}://{receivedUri.Host}{receivedUri.AbsolutePath}");
#endif
        if (Uri.TryCreate(url.AbsoluteString, UriKind.Absolute, out var uri))
            Microsoft.Maui.Controls.Application.Current?.SendOnAppLinkRequestReceived(uri);

        return true;
    }
}
