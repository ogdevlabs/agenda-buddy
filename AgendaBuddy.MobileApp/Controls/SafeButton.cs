#if MOBILE
namespace AgendaBuddy.MobileApp.Controls;

/// <summary>
/// A <see cref="Button"/> that cannot terminate the process when an iOS layout pass lands on it after its
/// handler has been disconnected.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect, and why the workaround is iOS-only.</b> <c>Button</c>'s
/// <c>ICrossPlatformLayout.CrossPlatformArrange</c> lives in the platform partial
/// <c>Button.iOS.cs</c>. It reads <c>Handler?.PlatformView as UIButton</c> — null-guarded — then passes
/// that possibly-null value into its private <c>LayoutButton</c>, which dereferences it with no guard. A
/// queued UIKit layout pass landing after teardown throws <see cref="NullReferenceException"/> out of a
/// native callback, which kills the app outright: it cannot be caught in app code, and
/// <c>Runtime.MarshalManagedException</c> is never raised for this path (verified — a handler on it never
/// fired across three separate crash reports). <c>LayoutButton</c> does not exist on Android, so there is
/// nothing to guard there.
/// </para>
/// <para>
/// The <c>#if IOS</c> is load-bearing, not tidiness. Re-declaring <c>ICrossPlatformLayout</c> re-maps the
/// interface on this type, and a base class's <i>explicit</i> implementations do not satisfy a derived
/// re-declaration. On Android <c>Button</c> gets its <c>ICrossPlatformLayout</c> members from a different
/// partial, so re-declaring it there demanded a <c>CrossPlatformMeasure</c> this class has no correct way
/// to supply — the Android build failed with CS0535 (caught by CI; local iOS-only builds could not see it).
/// </para>
/// <para>
/// Observed triggers all share "a Button is arranged while something else is tearing down or resizing": a
/// Shell pop, a <c>CollectionView</c> recycling a template, and a <c>DatePicker</c>/<c>TimePicker</c> input
/// accessory dismissing and resizing the KeyboardLayoutGuide. It is <b>not</b> related to <c>Padding</c>,
/// which an earlier round of this investigation wrongly concluded.
/// </para>
/// <para>
/// <b>Why the arrange is replaced outright rather than delegated.</b> There is no way to call the base
/// explicit implementation: <c>base</c> cannot reach one, and casting to <c>Button</c> does not change
/// interface dispatch — the runtime type's map still wins, so a "guard then delegate" version recurses
/// until the stack overflows. Returning the bounds is therefore the whole implementation.
/// </para>
/// <para>
/// <b>The tradeoff.</b> On iOS this skips <c>ContentLayout</c> — the image/text positioning maths
/// <c>LayoutButton</c> exists for. Every button in this app is text-only, so nothing is lost today; a
/// button that needs an image positioned relative to its text should use <c>Button</c> and accept the
/// crash risk, or set its insets explicitly.
/// </para>
/// </remarks>
public class SafeButton : Button
#if IOS
    , ICrossPlatformLayout
#endif
{
#if IOS
    // ONLY Arrange is re-implemented. CrossPlatformMeasure is deliberately left to the base: on iOS
    // Button satisfies it for a derived re-declaration, so measurement stays exactly as the framework
    // computes it. Supplying one here — returning the incoming constraints — collapsed real buttons off
    // the screen (the Account page's Edit and Change Password both disappeared), because a measure is
    // asked for a DESIRED size, not handed the available one.
    Size ICrossPlatformLayout.CrossPlatformArrange(Rect bounds) => new(bounds.Width, bounds.Height);
#endif
}
#endif
