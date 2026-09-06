namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// A push that arrived while the app was in the foreground, reduced to the three things the in-app banner
/// needs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists at all.</b> Neither platform's OS draws a notification banner for a push that arrives
/// while the app is on screen — Android hands the message to the app instead of the tray, and iOS asks the
/// app what to present and defaults to nothing. So without an in-app banner a notification arriving while
/// somebody is *using* the app is completely silent: the only trace is a row in a list they have to go and
/// open.
/// </para>
/// <para>
/// A plain record with no MAUI type, built by <see cref="From"/> from FCM's own string-to-string data
/// payload, so the decision about whether an arrival is worth showing is covered by the <c>net10.0</c> test
/// slice — where none of the Firebase or MAUI types exist.
/// </para>
/// </remarks>
public sealed record InAppNotification(string Title, string Body, string AppointmentIdentifier)
{
    /// <summary>Whether the banner can offer a way into the appointment rather than just the inbox.</summary>
    public bool HasAppointment => !string.IsNullOrWhiteSpace(AppointmentIdentifier);

    /// <summary>
    /// One line for the banner: the title, plus the body when there is one.
    /// </summary>
    /// <remarks>
    /// A single string because a snackbar has one text slot. The title leads, because a producer's subject is
    /// the summary and the body is the detail — truncation should cost the detail, not the point.
    /// </remarks>
    public string BannerText =>
        string.IsNullOrWhiteSpace(Body) ? Title : $"{Title} — {Body}";

    /// <summary>
    /// Builds one from an FCM message, or <c>null</c> when there is nothing worth showing.
    /// </summary>
    /// <remarks>
    /// A message with no title and no body is not a banner: FCM will deliver a data-only push (which is how a
    /// silent, purely-state-carrying message is sent), and drawing an empty banner for one is worse than
    /// drawing nothing. The appointment identifier is read under the same key the tap path uses, so the two
    /// cannot drift from each other or from <c>NotificationDispatcher</c>.
    /// </remarks>
    public static InAppNotification? From(
        string? title, string? body, IDictionary<string, string>? data, string appointmentKey)
    {
        var resolvedTitle = (title ?? string.Empty).Trim();
        var resolvedBody = (body ?? string.Empty).Trim();

        if (resolvedTitle.Length == 0 && resolvedBody.Length == 0)
            return null;

        var appointmentIdentifier = data is not null
                                    && data.TryGetValue(appointmentKey, out var identifier)
                                    && !string.IsNullOrWhiteSpace(identifier)
            ? identifier.Trim()
            : string.Empty;

        // A body with no title still deserves a banner; promote it rather than showing a blank first line.
        if (resolvedTitle.Length == 0)
            return new InAppNotification(resolvedBody, string.Empty, appointmentIdentifier);

        return new InAppNotification(resolvedTitle, resolvedBody, appointmentIdentifier);
    }
}
