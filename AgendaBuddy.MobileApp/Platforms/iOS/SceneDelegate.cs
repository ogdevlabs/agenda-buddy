using Foundation;
using UIKit;

namespace AgendaBuddy.MobileApp;

[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
    public override bool OpenUrl(UIScene scene, NSSet<UIOpenUrlContext> urlContexts)
    {
        var context = urlContexts.AnyObject;
        return context is not null
            ? AppLinkHandler.Open(context.Url)
            : base.OpenUrl(scene, urlContexts);
    }
}