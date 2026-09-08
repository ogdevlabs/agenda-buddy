namespace AgendaBuddy.Provider.Tests.Commands;

public class DeleteProviderAccountCommandHandlerTest
{
    private const string Email = "coach@example.com";

    private static AccountErasureSummary Summary(bool profileFound = true) => new(
        ProfileFound: profileFound,
        Tombstone: AccountErasure.NewTombstone(),
        AppointmentsAnonymised: 12,
        EmbeddedAppointmentsAnonymised: 0,
        MessagesAnonymised: 30,
        PaymentsAnonymised: 12,
        NotificationsDeleted: 40,
        NotesDeleted: 9,
        SubscriptionsRemoved: 4,
        DeviceTokensDeleted: 1);

    private static (DeleteProviderAccountCommandHandler Handler, Mock<IAccountErasureService> Erasure, Mock<IEventStore> Audit)
        Build(Func<AccountErasureSummary> erase)
    {
        var erasure = new Mock<IAccountErasureService>();
        erasure.Setup(e => e.EraseProviderAsync(It.IsAny<string>())).ReturnsAsync(erase);

        var eventStore = new Mock<IEventStore>();

        return (new DeleteProviderAccountCommandHandler(erasure.Object, eventStore.Object),
                erasure,
                eventStore);
    }

    [Fact]
    public async Task Handle_ErasesTheAccountAndAuditsSuccess()
    {
        var (handler, erasure, audit) = Build(() => Summary());

        var result = await handler.Handle(
            new DeleteProviderAccountCommand { Email = Email }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        erasure.Verify(e => e.EraseProviderAsync(Email), Times.Once);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Success" && ev.Type == "DeleteProviderAccountCommand")), Times.Once);
    }

    /// <summary>
    /// ⚠️ Deleting is NOT deactivating. This command must not be reachable by anything expecting
    /// <c>IsActive=false</c> semantics, and it must not fall back to them either: the erasure service is the only
    /// collaborator, so there is no path here that merely hides the provider.
    /// </summary>
    [Fact]
    public async Task Handle_NoProfileToRemove_StillSucceeds()
    {
        var (handler, _, audit) = Build(() => Summary(profileFound: false));

        var result = await handler.Handle(
            new DeleteProviderAccountCommand { Email = Email }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.ProfileFound);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success")), Times.Once);
    }

    /// <summary>The success record must not carry the address that was just erased.</summary>
    [Fact]
    public async Task Handle_TheSuccessAuditRecordDoesNotContainTheErasedAddress()
    {
        var (handler, _, audit) = Build(() => Summary());

        await handler.Handle(new DeleteProviderAccountCommand { Email = Email }, CancellationToken.None);

        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Success" && !ev.Data.Contains(Email, StringComparison.OrdinalIgnoreCase))), Times.Once);
    }

    /// <summary>
    /// The failure record does carry the address, deliberately: a half-finished erasure has to be completable, and
    /// it cannot be retried against an account nobody can name.
    /// </summary>
    [Fact]
    public async Task Handle_WhenErasureThrows_ItFailsAndTheFailureRecordNamesTheAccount()
    {
        var (handler, _, audit) = Build(() => throw new MongoException("unreachable"));

        var result = await handler.Handle(
            new DeleteProviderAccountCommand { Email = Email }, CancellationToken.None);

        Assert.True(result.IsFailed);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Failed"
                  && ev.Type == "DeleteProviderAccountCommand"
                  && ev.Data.Contains(Email, StringComparison.OrdinalIgnoreCase))), Times.Once);
    }
}
