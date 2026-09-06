namespace AgendaBuddy.Library.Services;

/// <summary>
/// The keys of the FCM <c>data</c> payload, shared by the sender and the client.
/// </summary>
/// <remarks>
/// <para>
/// One definition rather than a matching pair of string literals on either side of the network. Nothing else
/// couples <c>NotificationDispatcher</c> to <c>PushNotificationService</c>, so a rename on one side was
/// previously silent: the payload still arrived, the client read a key that was not there, and the feature
/// that depended on it simply did nothing. <c>AgendaBuddy.MobileApp</c> references this project, so it reads
/// these constants directly (see <c>PushNotificationService</c>).
/// </para>
/// <para>
/// <b>The <c>data</c> payload is where a notification's real content lives, and that is a security decision,
/// not a convenience.</b> The OS renders <c>notification.title</c>/<c>notification.body</c> on the lock screen
/// with no authentication, so those two carry nothing but a category (threat T-002). <c>data</c> is delivered
/// to the app rather than drawn by the OS, so the detail in <see cref="Subject"/>/<see cref="Body"/> is only
/// ever rendered inside the authenticated app.
/// </para>
/// </remarks>
public static class PushPayloadKeys
{
    /// <summary>The appointment a notification is about, so a tap can open it rather than just the app.</summary>
    public const string AppointmentIdentifier = "appointmentIdentifier";

    /// <summary>The producer's real headline. Rendered in-app only — never by the OS.</summary>
    public const string Subject = "subject";

    /// <summary>The producer's real detail line. Rendered in-app only — never by the OS.</summary>
    public const string Body = "body";
}
