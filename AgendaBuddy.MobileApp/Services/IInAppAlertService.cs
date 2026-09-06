namespace AgendaBuddy.MobileApp.Services;

/// <summary>
/// Shows a transient in-app message over whatever screen the user is on — the "toast" half of telling somebody
/// something happened without navigating them anywhere.
/// </summary>
/// <remarks>
/// An interface because <c>Toast</c> and <c>Snackbar</c> come from CommunityToolkit.Maui, which does not exist
/// on the <c>net10.0</c> test slice; behind this, view models and <c>PushNotificationService</c> stay testable.
/// Every method is best-effort and must never throw: a banner failing to draw cannot be allowed to take down
/// the notification, the appointment, or the screen that asked for it.
/// </remarks>
public interface IInAppAlertService
{
    /// <summary>A short confirmation that disappears on its own. No action, nothing to dismiss.</summary>
    Task ShowAsync(string message);

    /// <summary>
    /// A message with one action on it, for an arrival the reader may want to follow.
    /// </summary>
    /// <param name="actionLabel">The button's text, e.g. "View".</param>
    /// <param name="action">Run when the button is tapped; never run otherwise.</param>
    Task ShowAsync(string message, string actionLabel, Func<Task> action);
}
