namespace AgendaBuddy.Library.Services;

/// <summary>
/// Push delivery configuration. Absent a project id and credentials, push is disabled rather than broken —
/// see <see cref="UnconfiguredPushSender"/>, and the same shape as <see cref="EmailOptions"/>.
/// </summary>
public class PushOptions
{
    public const string Section = "Push";

    /// <summary>
    /// Firebase project id. When empty, no push is sent and nothing throws.
    /// </summary>
    public string? FirebaseProjectId { get; set; }

    /// <summary>
    /// The service-account JSON Firebase issues, as a single string. Required for FCM HTTP v1: the legacy
    /// server-key API this would otherwise use was shut down in 2024, so a bearer token minted from this
    /// credential is the only way in.
    /// </summary>
    public string? ServiceAccountJson { get; set; }

    /// <summary>
    /// The Android notification channel every message names.
    /// </summary>
    /// <remarks>
    /// A constant, not a setting: it has to equal the channel the client creates and declares in its manifest
    /// (<c>AgendaBuddy.MobileApp/Infrastructure/PushChannel.cs</c>, which reads this), and the two are shipped
    /// together. Naming a channel the app has not created is silent — Android posts to the one the Firebase SDK
    /// auto-creates instead, so every notification still arrives and the app's own channel settings do nothing.
    /// </remarks>
    public const string AndroidChannelId = "agendame_notifications";
}
