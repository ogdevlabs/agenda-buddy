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
    /// </summary>
    /// <remarks>
    /// ⚠️ <b><c>fererelabs.com</c> has to be verified in the Resend dashboard (domain, DKIM and SPF records)
    /// for this to deliver anything.</b> An unverified sending domain is rejected by Resend at send time, which
    /// <see cref="ResendEmailSender"/> absorbs by contract — so the symptom is silent: every email reports as
    /// dispatched and none arrives.
    /// <para>
    /// This used to default to <c>onboarding@resend.dev</c>, Resend's sandbox sender. That needs no verified
    /// domain but delivers <b>only</b> to the Resend account owner's own address, which is enough for a dev
    /// smoke test and nothing else — a real customer's password reset went nowhere.
    /// </para>
    /// </remarks>
    public string FromAddress { get; set; } = "AgendaMe@fererelabs.com";

    /// <summary>Display name on the From header.</summary>
    public string FromName { get; set; } = "AgendaMe";

    /// <summary>
    /// Base URL the confirmation and reset links point at. Empty means the message quotes the raw token
    /// for the recipient to paste into the app instead of offering a link.
    /// </summary>
    public string? AppLinkBaseUrl { get; set; }
}
