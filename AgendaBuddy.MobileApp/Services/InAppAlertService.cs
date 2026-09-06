#if MOBILE
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace AgendaBuddy.MobileApp.Services;

/// <inheritdoc cref="IInAppAlertService"/>
/// <remarks>
/// A <c>Snackbar</c> when there is an action to offer and a <c>Toast</c> when there is not — a toast has no
/// button, and a snackbar with nothing to tap is heavier chrome than the message deserves.
/// <para>
/// Both are marshalled to the main thread: an arriving push is delivered on whatever thread the Firebase SDK
/// used, and presenting UI off the main thread is a crash on both platforms. Every failure is swallowed for
/// the reason on the interface — a banner is the least important thing in any call stack it appears in.
/// </para>
/// </remarks>
public class InAppAlertService : IInAppAlertService
{
    /// <summary>Long enough to read a subject and reach for the button, short enough not to sit in the way.</summary>
    private static readonly TimeSpan ActionableDuration = TimeSpan.FromSeconds(6);

    public async Task ShowAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Toast.Make(message, ToastDuration.Short, textSize: 14).Show());
        }
        catch (Exception)
        {
            // Never the reason a caller fails. See IInAppAlertService.
        }
    }

    public async Task ShowAsync(string message, string actionLabel, Func<Task> action)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Snackbar.Make(
                    message,
                    // Fire-and-forget deliberately: Snackbar's action is a synchronous Action, and blocking on
                    // navigation inside it deadlocks the main thread it is dispatched on.
                    action: () => _ = action(),
                    actionButtonText: actionLabel,
                    duration: ActionableDuration,
                    visualOptions: BannerOptions()).Show());
        }
        catch (Exception)
        {
            // As above.
        }
    }

    /// <summary>
    /// The app's own colours rather than the platform default grey, so an arriving notification reads as part
    /// of this app and not as a system message about it.
    /// </summary>
    private static SnackbarOptions BannerOptions() => new()
    {
        BackgroundColor = Color.FromArgb("#3730A3"),
        TextColor = Colors.White,
        ActionButtonTextColor = Color.FromArgb("#FDE68A"),
        CornerRadius = new CornerRadius(12)
    };
}
#endif
