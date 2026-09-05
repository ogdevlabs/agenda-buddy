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
}
