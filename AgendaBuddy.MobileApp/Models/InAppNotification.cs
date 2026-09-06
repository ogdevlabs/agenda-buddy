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
    /// <para>
    /// <b>The detail comes from the <c>data</c> payload; the OS-displayed text is only a fallback.</b>
    /// <c>notification.title</c>/<c>notification.body</c> are deliberately generic — the OS renders those two on
    /// an unauthenticated lock screen, so they carry a category and nothing else (threat T-002, enforced in
    /// <c>NotificationDispatcher.DisplayText</c>). The producer's real subject and body travel in <c>data</c>,
    /// which the OS hands to the app rather than drawing, and this banner is inside the authenticated app — so it
    /// is the one surface entitled to show them. Reading the displayed strings here instead would make every
    /// in-app banner say "Appointment request — open the app for details" to somebody who already has it open.
    /// </para>
    /// <para>
    /// A message carrying nothing usable on either path is not a banner: FCM delivers a data-only push (how a
    /// silent, purely-state-carrying message is sent), and drawing an empty banner for one is worse than drawing
    /// nothing. Every key is supplied by the caller from <c>PushPayloadKeys</c>, the single definition the
    /// dispatcher writes against, so the two sides cannot drift.
    /// </para>
    /// </remarks>
    public static InAppNotification? From(
        string? title,
        string? body,
        IDictionary<string, string>? data,
        string appointmentKey,
        string subjectKey,
        string bodyKey)
    {
        // data first, the notification block second: the former is the real content, the latter the placeholder
        // that was safe to put on a lock screen.
        var resolvedTitle = FirstNonEmpty(Value(data, subjectKey), title);
        var resolvedBody = FirstNonEmpty(Value(data, bodyKey), body);

        if (resolvedTitle.Length == 0 && resolvedBody.Length == 0)
            return null;

        var appointmentIdentifier = Value(data, appointmentKey);

        // A body with no title still deserves a banner; promote it rather than showing a blank first line.
        if (resolvedTitle.Length == 0)
            return new InAppNotification(resolvedBody, string.Empty, appointmentIdentifier);

        return new InAppNotification(resolvedTitle, resolvedBody, appointmentIdentifier);
    }

    private static string Value(IDictionary<string, string>? data, string key) =>
        data is not null && data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : string.Empty;

    private static string FirstNonEmpty(string preferred, string? fallback) =>
        preferred.Length > 0 ? preferred : (fallback ?? string.Empty).Trim();
}
