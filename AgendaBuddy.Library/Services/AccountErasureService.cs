using AgendaBuddy.Library.Accounts;
using AgendaBuddy.Library.Media;
using AgendaBuddy.Library.Showcase;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    IRepository<DeviceTokenEntity> deviceTokens,
    IRepository<ProviderShowcaseEntity> showcases,
    IRepository<MediaRefEntity> mediaRefs,
    IRepository<ShowcaseVisitEntity> showcaseVisits,
    IRepository<GoCounterEntity> goCounters,
    IRepository<ShowcaseReportEntity> showcaseReports,
    IRepository<ShowcaseBlockEntity> showcaseBlocks,
    IBlobStore? blobStore = null,
    ILogger<AccountErasureService>? logger = null)
    : IAccountErasureService
{
    private readonly ILogger logger = logger ?? NullLogger<AccountErasureService>.Instance;

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

        // The customer's own side of the showcase: who they looked at, whom they hid and what they reported.
        var showcaseRowsDeleted =
            await showcaseVisits.DeleteManyAsync(new BsonDocument("customer_email", email))
            + await showcaseBlocks.DeleteManyAsync(new BsonDocument("customer_email", email))
            + await showcaseReports.DeleteManyAsync(new BsonDocument("reporter_email", email));

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
            DeviceTokensDeleted: deviceTokensDeleted,
            ShowcaseRowsDeleted: showcaseRowsDeleted);
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

        var showcaseRowsDeleted = await EraseProviderShowcaseAsync(email);

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
            DeviceTokensDeleted: deviceTokensDeleted,
            ShowcaseRowsDeleted: showcaseRowsDeleted);
    }

    /// <summary>
    /// Deletes everything the showcase holds about this provider, then their stored images.
    /// </summary>
    /// <remarks>
    /// Keyed on the provider's id, read before the profile goes, because every showcase row but one is keyed on
    /// it and nothing else links them to the address. The blob delete is <b>best-effort</b>: storage being down must
    /// not make an erasure fail — that would leave the account undeletable, which App Review forbids — and the
    /// media sweep removes every blob prefix whose provider document no longer exists, so the bytes still go.
    /// </remarks>
    private async Task<long> EraseProviderShowcaseAsync(string email)
    {
        var provider = await providers.FindOneAsync(SupportTools<ProviderEntity>.FilterByEmail(email));
        var showcase = await showcases.FindOneAsync(new BsonDocument("provider_email", email));
        var providerId = provider?.Id ?? showcase?.ProviderId;
        if (providerId is not { } id)
            return 0;

        var byProvider = new BsonDocument("provider_id", id);
        var deleted =
            await showcases.DeleteManyAsync(byProvider)
            + await mediaRefs.DeleteManyAsync(byProvider)
            + await showcaseVisits.DeleteManyAsync(byProvider)
            + await goCounters.DeleteManyAsync(byProvider)
            + await showcaseReports.DeleteManyAsync(byProvider)
            + await showcaseBlocks.DeleteManyAsync(byProvider)
            + await showcaseVisits.DeleteManyAsync(new BsonDocument("customer_email", email))
            + await showcaseBlocks.DeleteManyAsync(new BsonDocument("customer_email", email))
            + await showcaseReports.DeleteManyAsync(new BsonDocument("reporter_email", email));

        if (blobStore is not null)
        {
            try
            {
                await blobStore.DeletePrefixAsync(MediaKeys.Prefix(id));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Media for an erased provider could not be deleted now; the media sweep will remove it");
            }
        }

        return deleted;
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
