using AgendaBuddy.Library.Accounts;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

/// <summary>
/// Erasing an account: the profile goes, and every trace of who it belonged to is scrubbed out of records that
/// belong to <b>other people</b>.
/// </summary>
/// <remarks>
/// The write <i>shapes</i> are the subject here, not just the effects. An erasure touches the counterparty's
/// documents, so a whole-document replace would let one person's deletion discard a concurrent edit to somebody
/// else's appointment — which is exactly the failure a passing "the address is gone" assertion would hide.
/// </remarks>
public class AccountErasureServiceTest
{
    private const string CustomerEmail = "ada@example.com";
    private const string ProviderEmail = "coach@example.com";

    private readonly Mock<IRepository<CustomerEntity>> _customers = new();
    private readonly Mock<IRepository<ProviderEntity>> _providers = new();
    private readonly Mock<IRepository<AppointmentEntity>> _appointments = new();
    private readonly Mock<IRepository<MessageEntity>> _messages = new();
    private readonly Mock<IRepository<NotificationEntity>> _notifications = new();
    private readonly Mock<IRepository<NoteEntity>> _notes = new();
    private readonly Mock<IRepository<PaymentEntity>> _payments = new();
    private readonly Mock<IRepository<DeviceTokenEntity>> _deviceTokens = new();

    private AccountErasureService Service() => new(
        _customers.Object, _providers.Object, _appointments.Object, _messages.Object,
        _notifications.Object, _notes.Object, _payments.Object, _deviceTokens.Object);

    public AccountErasureServiceTest()
    {
        _appointments.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>()))
                     .ReturnsAsync([]);
    }

    // ── the customer path ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ErasingACustomerDeletesTheProfile()
    {
        _customers.Setup(r => r.FindOneAndDeleteAsync(It.IsAny<BsonDocument>()))
                  .ReturnsAsync(new CustomerEntity { Email = CustomerEmail });

        var summary = await Service().EraseCustomerAsync(CustomerEmail);

        Assert.True(summary.ProfileFound);
        _customers.Verify(r => r.FindOneAndDeleteAsync(
            It.Is<BsonDocument>(f => Filters(f, "email", CustomerEmail))), Times.Once);
    }

    /// <summary>
    /// ⚠️ <b>A missing profile is a success, not a 404.</b> An account whose profile creation failed at
    /// registration is the one that most needs deleting — it can sign in and has no profile — and treating "not
    /// found" as a failure would make exactly that account permanently undeletable.
    /// </summary>
    [Fact]
    public async Task ErasingAnAccountWithNoProfileStillSucceedsAndStillScrubs()
    {
        _customers.Setup(r => r.FindOneAndDeleteAsync(It.IsAny<BsonDocument>()))
                  .ReturnsAsync((CustomerEntity?)null);

        var summary = await Service().EraseCustomerAsync(CustomerEmail);

        Assert.False(summary.ProfileFound);
        _notifications.Verify(r => r.DeleteManyAsync(It.IsAny<BsonDocument>()), Times.Once);
        _appointments.Verify(r => r.UpdateManyAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()), Times.Once);
    }

    /// <summary>
    /// The inbox is the account's own, so it is deleted rather than anonymised — a scrubbed notification is a row
    /// addressed to nobody that nobody can read.
    /// </summary>
    [Fact]
    public async Task TheAccountsOwnNotificationsAreDeletedRatherThanAnonymised()
    {
        await Service().EraseCustomerAsync(CustomerEmail);

        _notifications.Verify(r => r.DeleteManyAsync(
            It.Is<BsonDocument>(f => Filters(f, "recipient_email", CustomerEmail))), Times.Once);
        _notifications.Verify(r => r.UpdateManyAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()), Times.Never);
    }

    /// <summary>
    /// A device token identifies hardware still in somebody's hand, so it has to stop being addressable — otherwise
    /// a deleted account keeps receiving push, subject and body included.
    /// </summary>
    [Fact]
    public async Task TheDeviceRegistrationIsRemoved()
    {
        await Service().EraseCustomerAsync(CustomerEmail);

        _deviceTokens.Verify(r => r.DeleteManyAsync(
            It.Is<BsonDocument>(f => Filters(f, "user_email", CustomerEmail))), Times.Once);
    }

    /// <summary>
    /// The appointment survives with the address replaced. Deleting it would delete the <b>provider's</b> record of
    /// a session they delivered and were paid for.
    /// </summary>
    [Fact]
    public async Task AppointmentsAreAnonymisedNotDeleted()
    {
        await Service().EraseCustomerAsync(CustomerEmail);

        _appointments.Verify(r => r.UpdateManyAsync(
            It.Is<BsonDocument>(f => Filters(f, "email_customer", CustomerEmail)),
            It.Is<BsonDocument>(u => IsTombstoneSet(u, "email_customer"))), Times.Once);
        _appointments.Verify(r => r.DeleteManyAsync(It.IsAny<BsonDocument>()), Times.Never);
    }

    /// <summary>
    /// ⚠️ <b>The provider's embedded copy is scrubbed per appointment identifier, not in one pass.</b> MongoDB's
    /// positional <c>$</c> rewrites only the first matching array element, so a single update filtered on the
    /// address would leave every appointment after the first still carrying it — and the embedded list is what the
    /// calendar actually reads.
    /// </summary>
    [Fact]
    public async Task EveryEmbeddedCopyIsScrubbed_NotJustTheFirst()
    {
        _appointments.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>()))
                     .ReturnsAsync(
                     [
                         new AppointmentEntity { Identifier = "appt-1", EmailCustomer = CustomerEmail, EmailProvider = ProviderEmail },
                         new AppointmentEntity { Identifier = "appt-2", EmailCustomer = CustomerEmail, EmailProvider = ProviderEmail },
                         new AppointmentEntity { Identifier = "appt-3", EmailCustomer = CustomerEmail, EmailProvider = ProviderEmail }
                     ]);
        _providers.Setup(r => r.UpdateManyAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
                  .ReturnsAsync(1);

        await Service().EraseCustomerAsync(CustomerEmail);

        foreach (var identifier in new[] { "appt-1", "appt-2", "appt-3" })
        {
            _providers.Verify(r => r.UpdateManyAsync(
                It.Is<BsonDocument>(f => Filters(f, "appointments.identifier", identifier)),
                It.Is<BsonDocument>(u => IsTombstoneSet(u, "appointments.$.email_customer"))), Times.Once);
        }
    }

    /// <summary>
    /// One tombstone per deletion, reused across every field — so a provider's twenty sessions still read as
    /// twenty sessions with one person, while retaining nothing about who that was.
    /// </summary>
    [Fact]
    public async Task OneTombstoneIsUsedForEveryScrubbedFieldOfOneDeletion()
    {
        var written = new List<string>();
        CaptureTombstones(written);

        var summary = await Service().EraseCustomerAsync(CustomerEmail);

        Assert.NotEmpty(written);
        Assert.All(written, value => Assert.Equal(summary.Tombstone, value));
    }

    /// <summary>
    /// The body of a message is the counterparty's copy of a conversation they took part in. The address is the
    /// identifier; that is what goes.
    /// </summary>
    [Fact]
    public async Task BothEndsOfEveryMessageAreAnonymisedAndTheBodyIsKept()
    {
        await Service().EraseCustomerAsync(CustomerEmail);

        _messages.Verify(r => r.UpdateManyAsync(
            It.Is<BsonDocument>(f => Filters(f, "sender_email", CustomerEmail)),
            It.Is<BsonDocument>(u => IsTombstoneSet(u, "sender_email"))), Times.Once);
        _messages.Verify(r => r.UpdateManyAsync(
            It.Is<BsonDocument>(f => Filters(f, "recipient_email", CustomerEmail)),
            It.Is<BsonDocument>(u => IsTombstoneSet(u, "recipient_email"))), Times.Once);
        _messages.Verify(r => r.DeleteManyAsync(It.IsAny<BsonDocument>()), Times.Never);
    }

    /// <summary>
    /// A subscription is a live relationship, not a historical record — so it is pulled rather than tombstoned. A
    /// provider's client list must not grow a row for somebody who left.
    /// </summary>
    [Fact]
    public async Task SubscriptionsArePulledRatherThanTombstoned()
    {
        await Service().EraseCustomerAsync(CustomerEmail);

        _providers.Verify(r => r.UpdateManyAsync(
            It.Is<BsonDocument>(f => Filters(f, "subscribed_customer_collection", CustomerEmail)),
            It.Is<BsonDocument>(u => u.Contains("$pull"))), Times.Once);
    }

    /// <summary>
    /// ⚠️ Every write is targeted (ADR-032). A whole-document replace here would let one person's deletion discard
    /// a concurrent edit to a <b>different</b> person's appointment or provider record.
    /// </summary>
    [Fact]
    public async Task NothingIsWrittenAsAWholeDocumentReplacement()
    {
        await Service().EraseCustomerAsync(CustomerEmail);
        await Service().EraseProviderAsync(ProviderEmail);

        _providers.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<ProviderEntity>()), Times.Never);
        _customers.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<CustomerEntity>()), Times.Never);
        _appointments.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<AppointmentEntity>()), Times.Never);
        _messages.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<MessageEntity>()), Times.Never);
    }

    /// <summary>
    /// The profile is removed <b>last</b>, so an interruption leaves an account that still exists and can be
    /// deleted again. The reverse order orphans rows still carrying the address with nothing to drive a retry from.
    /// </summary>
    [Fact]
    public async Task TheProfileIsDeletedAfterTheDependentTracesAreScrubbed()
    {
        var order = new List<string>();
        _appointments.Setup(r => r.UpdateManyAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
                     .ReturnsAsync(0)
                     .Callback(() => order.Add("scrub"));
        _customers.Setup(r => r.FindOneAndDeleteAsync(It.IsAny<BsonDocument>()))
                  .ReturnsAsync((CustomerEntity?)null)
                  .Callback(() => order.Add("profile"));

        await Service().EraseCustomerAsync(CustomerEmail);

        Assert.Equal(["scrub", "profile"], order);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyAddressIsRefusedRatherThanScrubbingEverything(string? email)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Service().EraseCustomerAsync(email!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Service().EraseProviderAsync(email!));
    }

    // ── the provider path ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A provider's session notes are private to them and are <b>about</b> their clients, so they go outright.
    /// Anonymising the provider's address on a note would leave the client's free text standing with no owner and
    /// no route to erase it.
    /// </summary>
    [Fact]
    public async Task ErasingAProviderDeletesTheirNotesOutright()
    {
        _notes.Setup(r => r.DeleteManyAsync(It.IsAny<BsonDocument>())).ReturnsAsync(3);

        var summary = await Service().EraseProviderAsync(ProviderEmail);

        _notes.Verify(r => r.DeleteManyAsync(
            It.Is<BsonDocument>(f => Filters(f, "provider_email", ProviderEmail))), Times.Once);
        Assert.Equal(3, summary.NotesDeleted);
    }

    /// <summary>A customer's own record of sessions they attended is theirs, so it is scrubbed, not removed.</summary>
    [Fact]
    public async Task ACustomersHistoryOfAProvidersSessionsSurvivesTheProvidersDeletion()
    {
        await Service().EraseProviderAsync(ProviderEmail);

        _appointments.Verify(r => r.UpdateManyAsync(
            It.Is<BsonDocument>(f => Filters(f, "email_provider", ProviderEmail)),
            It.Is<BsonDocument>(u => IsTombstoneSet(u, "email_provider"))), Times.Once);
        _appointments.Verify(r => r.DeleteManyAsync(It.IsAny<BsonDocument>()), Times.Never);
    }

    /// <summary>
    /// The provider document carries the services and the embedded appointment list, so both go with it — there is
    /// no separate collection to scrub, and no second pass over the embedded copies.
    /// </summary>
    [Fact]
    public async Task TheProviderDocumentTakesItsServicesAndEmbeddedAppointmentsWithIt()
    {
        var summary = await Service().EraseProviderAsync(ProviderEmail);

        _providers.Verify(r => r.FindOneAndDeleteAsync(
            It.Is<BsonDocument>(f => Filters(f, "email", ProviderEmail))), Times.Once);
        Assert.Equal(0, summary.EmbeddedAppointmentsAnonymised);
    }

    [Fact]
    public async Task ACustomersSubscriptionToADeletedProviderIsRemoved()
    {
        await Service().EraseProviderAsync(ProviderEmail);

        _customers.Verify(r => r.UpdateManyAsync(
            It.Is<BsonDocument>(f => Filters(f, "subscribed_provider_collection", ProviderEmail)),
            It.Is<BsonDocument>(u => u.Contains("$pull"))), Times.Once);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether <paramref name="filter"/> matches <paramref name="field"/> to <paramref name="value"/>.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Guards <c>Contains</c> before indexing, and that is not defensive noise.</b> Moq applies a
    /// <c>It.Is</c> predicate to <i>every</i> recorded invocation on the mock, so a <c>BsonDocument</c> indexer
    /// throws <see cref="KeyNotFoundException"/> the moment one of those invocations used a different filter —
    /// which turns "this call did not match" into a test error.
    /// </remarks>
    private static bool Filters(BsonDocument filter, string field, string value) =>
        filter.Contains(field) && filter[field].IsString && filter[field].AsString == value;

    /// <summary>Whether <paramref name="update"/> is a <c>$set</c> writing a tombstone into <paramref name="field"/>.</summary>
    private static bool IsTombstoneSet(BsonDocument update, string field) =>
        update.Contains("$set")
        && update["$set"].IsBsonDocument
        && update["$set"].AsBsonDocument.Contains(field)
        && update["$set"].AsBsonDocument[field].IsString
        && AccountErasure.IsTombstone(update["$set"].AsBsonDocument[field].AsString);

    /// <summary>Records every tombstone value written across the collections that get one.</summary>
    private void CaptureTombstones(List<string> written)
    {
        void Capture(BsonDocument update)
        {
            if (!update.Contains("$set")) return;

            foreach (var element in update["$set"].AsBsonDocument)
            {
                if (element.Value.IsString && AccountErasure.IsTombstone(element.Value.AsString))
                    written.Add(element.Value.AsString);
            }
        }

        _appointments.Setup(r => r.UpdateManyAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
                     .ReturnsAsync(1).Callback<BsonDocument, BsonDocument>((_, u) => Capture(u));
        _messages.Setup(r => r.UpdateManyAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
                 .ReturnsAsync(1).Callback<BsonDocument, BsonDocument>((_, u) => Capture(u));
        _payments.Setup(r => r.UpdateManyAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
                 .ReturnsAsync(1).Callback<BsonDocument, BsonDocument>((_, u) => Capture(u));
        _providers.Setup(r => r.UpdateManyAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
                  .ReturnsAsync(1).Callback<BsonDocument, BsonDocument>((_, u) => Capture(u));
    }
}
