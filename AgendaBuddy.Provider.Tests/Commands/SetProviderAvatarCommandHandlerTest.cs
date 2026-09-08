namespace AgendaBuddy.Provider.Tests.Commands;

public class SetProviderAvatarCommandHandlerTest
{
    private const string Email = "coach@example.com";

    private static ProviderEntity Provider(string avatarId = "") => new()
    {
        FirstName = "Grace",
        LastName = "Hopper",
        Email = Email,
        AvatarId = avatarId
    };

    private static (SetProviderAvatarCommandHandler Handler, Mock<IProviderService> Service, Mock<IEventStore> Audit)
        Build(ProviderEntity? updated)
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.SetAvatarAsync(It.IsAny<string>(), It.IsAny<string>()))
                       .ReturnsAsync(updated);

        var eventStore = new Mock<IEventStore>();

        return (new SetProviderAvatarCommandHandler(providerService.Object, eventStore.Object),
                providerService,
                eventStore);
    }

    [Fact]
    public async Task Handle_AKnownAvatar_WritesItAndAuditsSuccess()
    {
        var chosen = AvatarCatalog.Ids[6];
        var (handler, service, audit) = Build(Provider(chosen));

        var result = await handler.Handle(
            new SetProviderAvatarCommand { Email = Email, AvatarId = chosen }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(chosen, result.Value.AvatarId);
        service.Verify(p => p.SetAvatarAsync(Email, chosen), Times.Once);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Success" && ev.Type == "SetProviderAvatarCommand")), Times.Once);
    }

    /// <summary>
    /// ⚠️ <b>An unknown id is refused rather than stored.</b> Storing it throws nowhere —
    /// <c>AvatarCatalog.Resolve</c> treats an unknown id as absent and falls back to the email-derived mark — so
    /// the account would keep its old avatar with a successful save behind it.
    /// </summary>
    [Theory]
    [InlineData("avatar_99")]
    [InlineData("../../etc/passwd")]
    [InlineData("")]
    public async Task Handle_AnUnknownAvatar_IsRefusedAndNeverWritten(string avatarId)
    {
        var (handler, service, audit) = Build(Provider());

        var result = await handler.Handle(
            new SetProviderAvatarCommand { Email = Email, AvatarId = avatarId }, CancellationToken.None);

        Assert.True(result.IsFailed);
        service.Verify(p => p.SetAvatarAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Failed" && ev.Type == "SetProviderAvatarCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailAndAuditsTheFailure()
    {
        var (handler, _, audit) = Build(updated: null);

        var result = await handler.Handle(
            new SetProviderAvatarCommand { Email = "missing@example.com", AvatarId = AvatarCatalog.Ids[0] },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Failed" && ev.Type == "SetProviderAvatarCommand")), Times.Once);
    }
}
