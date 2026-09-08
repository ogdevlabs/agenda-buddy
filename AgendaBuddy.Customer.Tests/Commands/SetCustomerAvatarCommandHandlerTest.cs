namespace AgendaBuddy.Customer.Tests.Commands;

public class SetCustomerAvatarCommandHandlerTest
{
    private const string Email = "ada@example.com";

    private static (SetCustomerAvatarCommandHandler Handler, Mock<ICustomerService> Service, Mock<IEventStore> Audit)
        Build(CustomerEntity? updated)
    {
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(c => c.SetAvatarAsync(It.IsAny<string>(), It.IsAny<string>()))
                       .ReturnsAsync(updated);

        var eventStore = new Mock<IEventStore>();

        return (new SetCustomerAvatarCommandHandler(customerService.Object, eventStore.Object),
                customerService,
                eventStore);
    }

    [Fact]
    public async Task Handle_AKnownAvatar_WritesItAndAuditsSuccess()
    {
        var chosen = AvatarCatalog.Ids[6];
        var (handler, service, audit) = Build(new CustomerEntity { Email = Email, AvatarId = chosen });

        var result = await handler.Handle(
            new SetCustomerAvatarCommand { Email = Email, AvatarId = chosen }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(chosen, result.Value.AvatarId);
        service.Verify(c => c.SetAvatarAsync(Email, chosen), Times.Once);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Success" && ev.Type == "SetCustomerAvatarCommand")), Times.Once);
    }

    /// <summary>
    /// ⚠️ <b>An unknown id is refused, and it has to be refused here rather than trusted.</b> Storing it would
    /// throw nowhere: <c>AvatarCatalog.Resolve</c> treats an unknown id as absent and quietly falls back to the
    /// email-derived mark, so the account would keep showing its old avatar with a successful save behind it —
    /// indistinguishable from the feature not working.
    /// </summary>
    [Theory]
    [InlineData("avatar_99")]
    [InlineData("../../etc/passwd")]
    [InlineData("")]
    public async Task Handle_AnUnknownAvatar_IsRefusedAndNeverWritten(string avatarId)
    {
        var (handler, service, audit) = Build(new CustomerEntity { Email = Email });

        var result = await handler.Handle(
            new SetCustomerAvatarCommand { Email = Email, AvatarId = avatarId }, CancellationToken.None);

        Assert.True(result.IsFailed);
        service.Verify(c => c.SetAvatarAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Failed" && ev.Type == "SetCustomerAvatarCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchCustomer_ReturnsFailAndAuditsTheFailure()
    {
        var (handler, _, audit) = Build(updated: null);

        var result = await handler.Handle(
            new SetCustomerAvatarCommand { Email = "missing@example.com", AvatarId = AvatarCatalog.Ids[0] },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Failed" && ev.Type == "SetCustomerAvatarCommand")), Times.Once);
    }
}
