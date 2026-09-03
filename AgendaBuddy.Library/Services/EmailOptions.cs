namespace AgendaBuddy.Library.Services;

/// <summary>
/// Email delivery configuration. Absent an API key, delivery is disabled rather than broken — see
/// <see cref="ResendEmailSender"/>.
/// </summary>
public class EmailOptions
{
    public const string Section = "Email";

    /// <summary>Resend API key. When empty, no email is sent and nothing throws.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The From address. Must be on a domain verified with Resend, otherwise Resend rejects the send.
    /// <c>onboarding@resend.dev</c> works without a verified domain but only delivers to the Resend
    /// account owner's own address, which is enough for a dev smoke test and nothing else.
    /// </summary>
    public string FromAddress { get; set; } = "onboarding@resend.dev";

    /// <summary>Display name on the From header.</summary>
    public string FromName { get; set; } = "Agenda Buddy";

    /// <summary>
    /// Base URL the confirmation and reset links point at. Empty means the message quotes the raw token
    /// for the recipient to paste into the app instead of offering a link.
    /// </summary>
    public string? AppLinkBaseUrl { get; set; }
}
