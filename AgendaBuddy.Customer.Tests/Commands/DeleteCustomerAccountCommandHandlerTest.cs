namespace AgendaBuddy.Customer.Tests.Commands;

public class DeleteCustomerAccountCommandHandlerTest
{
    private const string Email = "ada@example.com";

    private static AccountErasureSummary Summary(bool profileFound = true) => new(
        ProfileFound: profileFound,
        Tombstone: AccountErasure.NewTombstone(),
        AppointmentsAnonymised: 2,
        EmbeddedAppointmentsAnonymised: 2,
        MessagesAnonymised: 5,
        PaymentsAnonymised: 1,
        NotificationsDeleted: 7,
        NotesDeleted: 0,
        SubscriptionsRemoved: 1,
        DeviceTokensDeleted: 1);

    private static (DeleteCustomerAccountCommandHandler Handler, Mock<IAccountErasureService> Erasure, Mock<IEventStore> Audit)
        Build(Func<AccountErasureSummary> erase)
    {
        var erasure = new Mock<IAccountErasureService>();
        erasure.Setup(e => e.EraseCustomerAsync(It.IsAny<string>()))
               .ReturnsAsync(erase);

        var eventStore = new Mock<IEventStore>();

        return (new DeleteCustomerAccountCommandHandler(erasure.Object, eventStore.Object),
                erasure,
                eventStore);
    }

    [Fact]
    public async Task Handle_ErasesTheAccountAndAuditsSuccess()
    {
        var (handler, erasure, audit) = Build(() => Summary());

        var result = await handler.Handle(
            new DeleteCustomerAccountCommand { Email = Email }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        erasure.Verify(e => e.EraseCustomerAsync(Email), Times.Once);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Success" && ev.Type == "DeleteCustomerAccountCommand")), Times.Once);
    }

    /// <summary>
    /// ⚠️ <b>An account with no profile still deletes.</b> Registration writes a credential and then a profile; if
    /// the second call fails, the account can sign in and has no profile — and that is precisely the account that
    /// must remain deletable (App Review Guideline 5.1.1(v)).
    /// </summary>
    [Fact]
    public async Task Handle_NoProfileToRemove_StillSucceeds()
    {
        var (handler, _, audit) = Build(() => Summary(profileFound: false));

        var result = await handler.Handle(
            new DeleteCustomerAccountCommand { Email = Email }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.ProfileFound);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success")), Times.Once);
    }

    /// <summary>
    /// ⚠️ <b>The success record must not carry the address that was just erased.</b> An audit trail holding what
    /// was deleted next to whose it was is a copy of the data the deletion removed. The tombstone is what
    /// correlates the rows instead.
    /// </summary>
    [Fact]
    public async Task Handle_TheSuccessAuditRecordDoesNotContainTheErasedAddress()
    {
        var (handler, _, audit) = Build(() => Summary());

        await handler.Handle(new DeleteCustomerAccountCommand { Email = Email }, CancellationToken.None);

        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Success" && !ev.Data.Contains(Email, StringComparison.OrdinalIgnoreCase))), Times.Once);
    }

    /// <summary>
    /// The failure record <b>does</b> carry the address, deliberately: a half-finished erasure has to be
    /// completable, and it cannot be retried against an account nobody can name.
    /// </summary>
    [Fact]
    public async Task Handle_WhenErasureThrows_ItFailsAndTheFailureRecordNamesTheAccount()
    {
        var (handler, _, audit) = Build(() => throw new MongoException("unreachable"));

        var result = await handler.Handle(
            new DeleteCustomerAccountCommand { Email = Email }, CancellationToken.None);

        Assert.True(result.IsFailed);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Failed"
                  && ev.Type == "DeleteCustomerAccountCommand"
                  && ev.Data.Contains(Email, StringComparison.OrdinalIgnoreCase))), Times.Once);
    }
}
