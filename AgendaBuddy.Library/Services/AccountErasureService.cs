using AgendaBuddy.Library.Accounts;

namespace AgendaBuddy.Library.Services;

/// <inheritdoc />
/// <remarks>
/// <para>
/// Every write here is a targeted <c>$set</c>, <c>$pull</c> or a delete (ADR-032) — never a whole-document
/// replacement. That matters more here than anywhere else: an erasure touches documents belonging to <b>other
/// people</b>, so a read-modify-replace would let one person's deletion discard a concurrent edit to the
/// counterparty's appointment.
/// </para>
/// <para>
/// <b>There is no transaction, and the order is chosen for what a half-finished erasure leaves behind.</b>
/// Dependent traces are scrubbed first and the profile is removed last, so an interruption leaves an account that
/// still exists and can be deleted again — the operation is idempotent, because a second run finds the already
/// scrubbed rows no longer matching the real address. The reverse order would leave orphaned rows still carrying
/// the address with no profile to drive a retry from.
/// </para>
/// </remarks>
public class AccountErasureService(
    IRepository<CustomerEntity> customers,
    IRepository<ProviderEntity> providers,
    IRepository<AppointmentEntity> appointments,
    IRepository<MessageEntity> messages,
    IRepository<NotificationEntity> notifications,
    IRepository<NoteEntity> notes,
    IRepository<PaymentEntity> payments,
    IRepository<DeviceTokenEntity> deviceTokens)
    : IAccountErasureService
{
    public async Task<AccountErasureSummary> EraseCustomerAsync(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var tombstone = AccountErasure.NewTombstone();

        // The inbox is the account's own, so it goes rather than being anonymised — a scrubbed notification is
        // a row nobody can ever read addressed to nobody.
        var notificationsDeleted = await notifications.DeleteManyAsync(
            new BsonDocument("recipient_email", email));

        var deviceTokensDeleted = await deviceTokens.DeleteManyAsync(
            new BsonDocument("user_email", email));

        // Read the identifiers BEFORE the scrub, because they are the only handle on the provider's embedded
        // copies once the address is gone from the canonical rows.
        var identifiers = (await appointments.FindAllAsync(new BsonDocument("email_customer", email)))
            .Select(appointment => appointment.Identifier)
            .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
            .Distinct()
            .ToList();

        var appointmentsAnonymised = await appointments.UpdateManyAsync(
            new BsonDocument("email_customer", email),
            new BsonDocument("$set", new BsonDocument("email_customer", tombstone)));

        var embeddedAnonymised = await AnonymiseEmbeddedAppointmentsAsync(identifiers, tombstone);

        var messagesAnonymised = await AnonymiseMessagesAsync(email, tombstone);

        var paymentsAnonymised = await payments.UpdateManyAsync(
            new BsonDocument("customer_email", email),
            new BsonDocument("$set", new BsonDocument("customer_email", tombstone)));

        // $pull rather than a tombstone: a subscription is a live relationship, not a historical record, and a
        // provider's client list should not grow a row for somebody who left.
        var subscriptionsRemoved = await providers.UpdateManyAsync(
            new BsonDocument("subscribed_customer_collection", email),
            new BsonDocument("$pull", new BsonDocument("subscribed_customer_collection", email)));

        var profile = await customers.FindOneAndDeleteAsync(SupportTools<CustomerEntity>.FilterByEmail(email));

        return new AccountErasureSummary(
            ProfileFound: profile is not null,
            Tombstone: tombstone,
            AppointmentsAnonymised: appointmentsAnonymised,
            EmbeddedAppointmentsAnonymised: embeddedAnonymised,
            MessagesAnonymised: messagesAnonymised,
            PaymentsAnonymised: paymentsAnonymised,
            NotificationsDeleted: notificationsDeleted,
            NotesDeleted: 0,
            SubscriptionsRemoved: subscriptionsRemoved,
            DeviceTokensDeleted: deviceTokensDeleted);
    }

    public async Task<AccountErasureSummary> EraseProviderAsync(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var tombstone = AccountErasure.NewTombstone();

        var notificationsDeleted = await notifications.DeleteManyAsync(
            new BsonDocument("recipient_email", email));

        var deviceTokensDeleted = await deviceTokens.DeleteManyAsync(
            new BsonDocument("user_email", email));

        // A provider's session notes are private to them and are about their clients, so they are deleted
        // outright. Anonymising the provider's address on a note would leave the client's medical- or
        // coaching-grade free text standing with no owner and no route to erase it.
        var notesDeleted = await notes.DeleteManyAsync(new BsonDocument("provider_email", email));

        var appointmentsAnonymised = await appointments.UpdateManyAsync(
            SupportTools<AppointmentEntity>.FilterByEmailProvider(email),
            new BsonDocument("$set", new BsonDocument("email_provider", tombstone)));

        var messagesAnonymised = await AnonymiseMessagesAsync(email, tombstone);

        var paymentsAnonymised = await payments.UpdateManyAsync(
            new BsonDocument("provider_email", email),
            new BsonDocument("$set", new BsonDocument("provider_email", tombstone)));

        var subscriptionsRemoved = await customers.UpdateManyAsync(
            new BsonDocument("subscribed_provider_collection", email),
            new BsonDocument("$pull", new BsonDocument("subscribed_provider_collection", email)));

        // Last, and it takes the services and the embedded appointment list with it — both are fields of this
        // document, so there is no separate collection to scrub.
        var profile = await providers.FindOneAndDeleteAsync(SupportTools<ProviderEntity>.FilterByEmail(email));

        return new AccountErasureSummary(
            ProfileFound: profile is not null,
            Tombstone: tombstone,
            AppointmentsAnonymised: appointmentsAnonymised,
            EmbeddedAppointmentsAnonymised: 0,
            MessagesAnonymised: messagesAnonymised,
            PaymentsAnonymised: paymentsAnonymised,
            NotificationsDeleted: notificationsDeleted,
            NotesDeleted: notesDeleted,
            SubscriptionsRemoved: subscriptionsRemoved,
            DeviceTokensDeleted: deviceTokensDeleted);
    }

    /// <summary>
    /// Rewrites the customer address on the provider's embedded copy of each of these appointments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The provider document carries its own copy of every appointment, and the calendar reads from that copy —
    /// so leaving it alone would keep the erased address on screen for the one person most likely to be looking
    /// at it.
    /// </para>
    /// <para>
    /// <b>One update per appointment identifier, deliberately.</b> MongoDB's positional <c>$</c> rewrites only
    /// the first array element the filter matched, so a single pass would leave every appointment after the first
    /// carrying the real address. Matching on the identifier — unique per appointment — makes each pass hit
    /// exactly the intended element. The alternative, <c>$[]</c>, would rewrite <i>every</i> customer's address
    /// in that provider's list; <c>$[element]</c> with an <c>arrayFilters</c> option would be one round trip but
    /// needs a primitive <see cref="IRepository{T}"/> deliberately does not have.
    /// </para>
    /// </remarks>
    private async Task<long> AnonymiseEmbeddedAppointmentsAsync(IEnumerable<string> identifiers, string tombstone)
    {
        long anonymised = 0;

        foreach (var identifier in identifiers)
        {
            anonymised += await providers.UpdateManyAsync(
                new BsonDocument("appointments.identifier", identifier),
                new BsonDocument("$set", new BsonDocument("appointments.$.email_customer", tombstone)));
        }

        return anonymised;
    }

    /// <summary>
    /// Rewrites the address on both ends of every message the account sent or received.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two updates rather than one <c>$or</c>, because the field to rewrite differs per match and MongoDB has no
    /// way to express "set whichever of these two fields matched" in a single update document.
    /// </para>
    /// <para>
    /// The message <b>body is kept</b>. It is the counterparty's copy of a conversation they took part in, and
    /// discarding it would delete their record rather than the leaver's identity. The address, which is the
    /// identifier, is what goes.
    /// </para>
    /// </remarks>
    private async Task<long> AnonymiseMessagesAsync(string email, string tombstone)
    {
        var sent = await messages.UpdateManyAsync(
            new BsonDocument("sender_email", email),
            new BsonDocument("$set", new BsonDocument("sender_email", tombstone)));

        var received = await messages.UpdateManyAsync(
            new BsonDocument("recipient_email", email),
            new BsonDocument("$set", new BsonDocument("recipient_email", tombstone)));

        return sent + received;
    }
}
