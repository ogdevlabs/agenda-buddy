namespace AgendaBuddy.Library.Services;

/// <summary>
/// Delivers transactional email. The only messages that need it are the ones carrying a token the
/// recipient cannot get any other way — email confirmation and password reset.
/// </summary>
/// <remarks>
/// Separate from <see cref="INotificationService"/> on purpose. A notification is an in-app inbox row the
/// recipient reads once they are already signed in; that is useless for a password reset, whose whole point
/// is reaching someone who cannot sign in. The two are sent together and neither replaces the other.
/// </remarks>
public interface IEmailSender
{
    /// <summary>
    /// Sends one message. Implementations must not throw on a delivery failure: nothing here is worth
    /// failing a registration or a reset request over, and a reset request that 500s tells an attacker
    /// the address exists.
    /// </summary>
    /// <returns><c>true</c> when the provider accepted the message.</returns>
    Task<bool> SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default);
}
