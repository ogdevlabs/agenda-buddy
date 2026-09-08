namespace AgendaBuddy.Library.Accounts;

/// <summary>
/// What an account erasure did, for the audit event.
/// </summary>
/// <remarks>
/// Alongside <see cref="AccountErasure"/> rather than beside the service interface, so a command in a service's
/// <c>*.Domain</c> project can be typed against it without <c>*.Domain</c> taking a dependency on the service
/// layer it is meant to sit underneath.
/// </remarks>
/// <param name="ProfileFound">
/// Whether there was a domain profile to remove. <c>false</c> is not a failure — an account whose profile
/// creation failed (or which predates profile creation entirely) still has a credential to delete and traces to
/// scrub, and refusing to erase it would leave the one class of account that most needs erasing unerasable.
/// </param>
/// <param name="Tombstone">
/// The address every scrubbed field was rewritten to. Recorded so the audit trail can answer "which rows were
/// this deletion's" without itself holding the address that was erased.
/// </param>
public record AccountErasureSummary(
    bool ProfileFound,
    string Tombstone,
    long AppointmentsAnonymised,
    long EmbeddedAppointmentsAnonymised,
    long MessagesAnonymised,
    long PaymentsAnonymised,
    long NotificationsDeleted,
    long NotesDeleted,
    long SubscriptionsRemoved,
    long DeviceTokensDeleted);
