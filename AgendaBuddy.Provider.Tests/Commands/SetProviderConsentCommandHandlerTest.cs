namespace AgendaBuddy.Provider.Tests.Commands;

public class SetProviderConsentCommandHandlerTest
{
    private const string Email = "coach@example.com";

    private static ProviderEntity Provider() => new()
    {
        FirstName = "Grace",
        LastName = "Hopper",
        Email = Email
    };

    private static (SetProviderConsentCommandHandler Handler, Mock<IProviderService> Service, Mock<IEventStore> Audit)
        Build(ProviderEntity? updated)
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.SetConsentAsync(
                           It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                       .ReturnsAsync(updated);

        var eventStore = new Mock<IEventStore>();

        return (new SetProviderConsentCommandHandler(providerService.Object, eventStore.Object),
                providerService,
                eventStore);
    }

    /// <summary>
    /// ⚠️ <b>The timestamp is the server's.</b> A consent record whose date the consenting party supplies proves
    /// nothing, which is why the command carries booleans and no dates.
    /// </summary>
    [Fact]
    public async Task Handle_AcceptingBoth_StampsTheServersOwnClock()
    {
        var (handler, service, audit) = Build(Provider());
        var before = DateTime.UtcNow;

        var result = await handler.Handle(
            new SetProviderConsentCommand { Email = Email, AcceptedTerms = true, AcceptedPrivacy = true },
            CancellationToken.None);

        var after = DateTime.UtcNow;

        Assert.True(result.IsSuccess);
        service.Verify(p => p.SetConsentAsync(
            Email,
            It.Is<DateTime?>(at => at >= before && at <= after),
            It.Is<DateTime?>(at => at >= before && at <= after)), Times.Once);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Success" && ev.Type == "SetProviderConsentCommand")), Times.Once);
    }

    /// <summary>Declining passes null, so an unticked box clears a previous acceptance instead of leaving it.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Handle_Declining_PassesNullSoThePreviousAcceptanceIsCleared(bool terms, bool privacy)
    {
        var (handler, service, _) = Build(Provider());

        await handler.Handle(
            new SetProviderConsentCommand { Email = Email, AcceptedTerms = terms, AcceptedPrivacy = privacy },
            CancellationToken.None);

        service.Verify(p => p.SetConsentAsync(
            Email,
            It.Is<DateTime?>(at => (at != null) == terms),
            It.Is<DateTime?>(at => (at != null) == privacy)), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailAndAuditsTheFailure()
    {
        var (handler, _, audit) = Build(updated: null);

        var result = await handler.Handle(
            new SetProviderConsentCommand { Email = "missing@example.com", AcceptedTerms = true, AcceptedPrivacy = true },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Failed" && ev.Type == "SetProviderConsentCommand")), Times.Once);
    }
}
