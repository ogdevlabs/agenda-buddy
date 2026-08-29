#if MOBILE
namespace AgendaBuddy.MobileApp.Controls;

/// <summary>
/// A <see cref="Button"/> that cannot terminate the process when a layout pass lands on it after its
/// handler has been disconnected.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> <c>Button</c>'s own <c>ICrossPlatformLayout.CrossPlatformArrange</c> reads
/// <c>Handler?.PlatformView as UIButton</c> — null-guarded — and then passes that possibly-null value
/// straight into its private <c>LayoutButton</c>, which dereferences it with no guard. A queued UIKit
/// layout pass that lands after teardown throws <see cref="NullReferenceException"/> out of a native
/// callback, which kills the app outright: it cannot be caught in app code, and
/// <c>Runtime.MarshalManagedException</c> is never raised for this path (verified — a handler on it
/// never fired across three separate crash reports).
/// </para>
/// <para>
/// Observed triggers all share "a Button is arranged while something else is tearing down or resizing":
/// a Shell pop, a <c>CollectionView</c> recycling a template, and a <c>DatePicker</c>/<c>TimePicker</c>
/// input accessory dismissing and resizing the KeyboardLayoutGuide. It is <b>not</b> related to
/// <c>Padding</c>, which an earlier round of this investigation wrongly concluded.
/// </para>
/// <para>
/// <b>Why the arrange is replaced outright rather than delegated.</b> Reimplementing the interface on a
/// subclass takes priority over the base class's explicit implementation, but there is no way to then
/// call the base one: <c>base</c> cannot reach an explicit interface implementation, and casting to
/// <c>Button</c> does not change interface dispatch — the runtime type's map still wins, so a
/// "guard then delegate" version recurses into itself until the stack overflows. Returning the bounds is
/// therefore the whole implementation.
/// </para>
/// <para>
/// <b>The tradeoff.</b> This skips <c>ContentLayout</c> — the image/text positioning maths
/// <c>LayoutButton</c> exists for. Every button in this app is text-only, so nothing is lost today; a
/// button that needs an image positioned relative to its text should use <c>Button</c> and accept the
/// crash risk, or set its insets explicitly.
/// </para>
/// </remarks>
public class SafeButton : Button, ICrossPlatformLayout
{
    Size ICrossPlatformLayout.CrossPlatformArrange(Rect bounds) => new(bounds.Width, bounds.Height);
}
#endif
