using Foundation;
using ObjCRuntime;

namespace AgendaBuddy.MobileApp;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp()
    {
        // .NET MAUI iOS framework bug: Button.LayoutButton (called from
        // Button.CrossPlatformArrange during any native layout pass — Shell back-nav pop,
        // a keyboard/date-picker accessory view docking, etc.) throws a NullReferenceException
        // unrelated to that button's own Padding/Text/content. Left unhandled, .NET for iOS
        // marshals it to an NSException and aborts the whole process. Swallow only this
        // specific known-bad framework path; every other managed exception still aborts normally.
        Runtime.MarshalManagedException += (_, args) =>
        {
            if (args.Exception is NullReferenceException
                && args.Exception.StackTrace?.Contains("Microsoft.Maui.Controls.Button.LayoutButton") == true)
            {
                Console.WriteLine("[AgendaBuddy] Swallowed known MAUI Button.LayoutButton NullReferenceException.");
                args.ExceptionMode = MarshalManagedExceptionMode.UnwindNativeCode;
            }
        };

        return MauiProgram.CreateMauiApp();
    }
}
