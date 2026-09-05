namespace AgendaBuddy.Library.Services;

/// <summary>
/// Delivers a push notification to one registered device.
/// </summary>
/// <remarks>
/// The third channel, alongside <see cref="INotificationService"/> (an inbox row the recipient reads once
/// signed in) and <see cref="IEmailSender"/> (reaches someone who is not in the app at all). Push is the one
/// that arrives while the app is closed but installed, which is what a booking request needs.
/// <para>
/// Implementations must not throw on a delivery failure, for the same reason <see cref="IEmailSender"/> must
/// not: nothing here is worth failing the appointment that triggered it.
/// </para>
/// </remarks>
public interface IPushSender
{
    /// <summary>
    /// Sends one message to <paramref name="deviceToken"/>.
    /// </summary>
    /// <param name="data">
    /// Key/value payload delivered alongside the visible message, for the client to act on when the
    /// notification is tapped. Carries <c>appointmentIdentifier</c> where there is one.
    /// </param>
    /// <returns><c>true</c> when the provider accepted the message.</returns>
    Task<bool> SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
