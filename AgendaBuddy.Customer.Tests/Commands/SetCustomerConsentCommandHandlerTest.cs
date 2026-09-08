namespace AgendaBuddy.Customer.Tests.Commands;

public class SetCustomerConsentCommandHandlerTest
{
    private const string Email = "ada@example.com";

    private static (SetCustomerConsentCommandHandler Handler, Mock<ICustomerService> Service, Mock<IEventStore> Audit)
        Build(CustomerEntity? updated)
    {
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.SetConsentAsync(
                           It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                       .ReturnsAsync(updated);

        var eventStore = new Mock<IEventStore>();

        return (new SetCustomerConsentCommandHandler(customerService.Object, eventStore.Object),
                customerService,
                eventStore);
    }

    /// <summary>
    /// ⚠️ <b>The timestamp is the server's.</b> A consent record whose date the consenting party supplies proves
    /// nothing, and a device with a wrong clock would file the acceptance in the wrong year — which is why the
    /// command carries booleans and no dates at all.
    /// </summary>
    [Fact]
    public async Task Handle_AcceptingBoth_StampsTheServersOwnClock()
    {
        var (handler, service, audit) = Build(new CustomerEntity { Email = Email });
        var before = DateTime.UtcNow;

        var result = await handler.Handle(
            new SetCustomerConsentCommand { Email = Email, AcceptedTerms = true, AcceptedPrivacy = true },
            CancellationToken.None);

        var after = DateTime.UtcNow;

        Assert.True(result.IsSuccess);
        service.Verify(c => c.SetConsentAsync(
            Email,
            It.Is<DateTime?>(at => at >= before && at <= after),
            It.Is<DateTime?>(at => at >= before && at <= after)), Times.Once);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Success" && ev.Type == "SetCustomerConsentCommand")), Times.Once);
    }

    /// <summary>
    /// Both timestamps come from one reading of the clock, so the two acceptances made in one action cannot be
    /// recorded a millisecond apart and read as two separate decisions.
    /// </summary>
    [Fact]
    public async Task Handle_AcceptingBoth_UsesOneInstantForBoth()
    {
        var (handler, service, _) = Build(new CustomerEntity { Email = Email });

        await handler.Handle(
            new SetCustomerConsentCommand { Email = Email, AcceptedTerms = true, AcceptedPrivacy = true },
            CancellationToken.None);

        service.Verify(c => c.SetConsentAsync(Email, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
        service.Verify(c => c.SetConsentAsync(
            Email,
            It.Is<DateTime?>(terms => terms != null),
            It.Is<DateTime?>(privacy => privacy != null)), Times.Once);
    }

    /// <summary>
    /// Declining passes <c>null</c>, which the service writes as an explicit null — so an unticked box clears a
    /// previous acceptance rather than silently leaving it standing.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Handle_Declining_PassesNullSoThePreviousAcceptanceIsCleared(bool terms, bool privacy)
    {
        var (handler, service, _) = Build(new CustomerEntity { Email = Email });

        await handler.Handle(
            new SetCustomerConsentCommand { Email = Email, AcceptedTerms = terms, AcceptedPrivacy = privacy },
            CancellationToken.None);

        service.Verify(c => c.SetConsentAsync(
            Email,
            It.Is<DateTime?>(at => (at != null) == terms),
            It.Is<DateTime?>(at => (at != null) == privacy)), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchCustomer_ReturnsFailAndAuditsTheFailure()
    {
        var (handler, _, audit) = Build(updated: null);

        var result = await handler.Handle(
            new SetCustomerConsentCommand { Email = "missing@example.com", AcceptedTerms = true, AcceptedPrivacy = true },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Failed" && ev.Type == "SetCustomerConsentCommand")), Times.Once);
    }
}
