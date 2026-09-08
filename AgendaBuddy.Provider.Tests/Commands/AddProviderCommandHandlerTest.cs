namespace AgendaBuddy.Provider.Tests.Commands;

public class AddProviderCommandHandlerTest
{
    private static ProviderEntity Provider(string firstName = "Grace", string lastName = "Hopper", string email = "provider@example.com") => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Email = email
    };

    [Fact]
    public async Task Handle_NoDuplicate_PersistsAndReturnsOk()
    {
        var provider = Provider();
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new AddProviderCommand { ProviderEntity = provider }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        providerService.Verify(p => p.AddProviderAsync(provider), Times.Once);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "AddProviderCommand")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Creating a provider reaches no message broker and cannot be made to fail by one being absent.
    /// Topic-per-provider creation used to run here and returned a failure when no broker answered,
    /// which made an unreachable broker block signup outright.
    /// </summary>
    [Fact]
    public async Task Handle_SucceedsWithNoMessageBrokerAnywhere()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var handler = new AddProviderCommandHandler(
            Mock.Of<IMediator>(), providerService.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(new AddProviderCommand { ProviderEntity = Provider() }, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_DuplicateEmailFound_ReturnsFailWithNoPublishAndNoAuditWrite()
    {
        // The duplicate check runs BEFORE mediator.Publish or any event store write -- a duplicate
        // never touches either.
        var provider = Provider();
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync(Provider());
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new AddProviderCommand { ProviderEntity = provider }, CancellationToken.None);

        Assert.True(result.IsFailed);
        providerService.Verify(p => p.AddProviderAsync(It.IsAny<ProviderEntity>()), Times.Never);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.IsAny<Event>()), Times.Never);
    }

    // ── The email is the identity; a name is not ────────────────────────────────────────────────────

    // Uniqueness is on the EMAIL and nothing else. Matching on first+last name refused a genuinely new
    // account because somebody already shared its holder's name, and reported it as a conflict on an
    // address that was not in use — so every second "John Smith" could never have a profile at all.
    [Fact]
    public async Task Handle_ChecksForADuplicateByEmailAndNotByName()
    {
        BsonDocument? filter = null;
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<BsonDocument>()))
                       .Callback<BsonDocument>(f => filter = f)
                       .ReturnsAsync((ProviderEntity)null!);
        var handler = new AddProviderCommandHandler(
            Mock.Of<IMediator>(), providerService.Object, Mock.Of<IEventStore>());

        await handler.Handle(
            new AddProviderCommand { ProviderEntity = Provider(email: "grace@example.com") },
            CancellationToken.None);

        Assert.NotNull(filter);
        Assert.Equal("grace@example.com", filter!["email"].AsString);
        Assert.False(filter.Contains("first_name"));
        Assert.False(filter.Contains("last_name"));
    }

    // A person holds several addresses and any number of people share a name, so neither of these is a
    // reason to refuse an account.
    [Fact]
    public async Task Handle_SameNameDifferentEmail_IsCreated()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.Is<BsonDocument>(f => f["email"] == "grace.second@example.com")))
                       .ReturnsAsync((ProviderEntity)null!);
        var handler = new AddProviderCommandHandler(
            Mock.Of<IMediator>(), providerService.Object, Mock.Of<IEventStore>());

        var second = Provider(firstName: "Grace", lastName: "Hopper", email: "grace.second@example.com");
        var result = await handler.Handle(new AddProviderCommand { ProviderEntity = second }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        providerService.Verify(p => p.AddProviderAsync(second), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new AddProviderCommandHandler(
            Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }

    // ── Avatar assignment ───────────────────────────────────────────────────────────────────────────
    // Assigned here, at creation, because it has to be stable for the life of the account. The client falls
    // back to a derivation from the email when this is empty, so a missing assignment is invisible rather than
    // broken -- which is exactly why it needs a test.

    [Fact]
    public async Task Handle_AssignsAnAvatarFromTheCatalog()
    {
        var provider = Provider();
        var providerService = new Mock<IProviderService>();
        providerService.Setup(c => c.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var handler = new AddProviderCommandHandler(
            Mock.Of<IMediator>(), providerService.Object, Mock.Of<IEventStore>());

        await handler.Handle(new AddProviderCommand { ProviderEntity = provider }, CancellationToken.None);

        Assert.Contains(provider.AvatarId, AvatarCatalog.Ids);
    }

    // A caller that chose one has chosen deliberately; creation must not overwrite it.
    [Fact]
    public async Task Handle_KeepsAnAvatarTheCallerAlreadyChose()
    {
        var provider = Provider();
        provider.AvatarId = "avatar_11";
        var providerService = new Mock<IProviderService>();
        providerService.Setup(c => c.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var handler = new AddProviderCommandHandler(
            Mock.Of<IMediator>(), providerService.Object, Mock.Of<IEventStore>());

        await handler.Handle(new AddProviderCommand { ProviderEntity = provider }, CancellationToken.None);

        Assert.Equal("avatar_11", provider.AvatarId);
    }

    // An id this build does not ship is replaced rather than stored, so the client is never asked for a
    // missing asset.
    [Fact]
    public async Task Handle_ReplacesAnAvatarIdItDoesNotRecognise()
    {
        var provider = Provider();
        provider.AvatarId = "avatar_99";
        var providerService = new Mock<IProviderService>();
        providerService.Setup(c => c.FindProvidersAsync(It.IsAny<BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var handler = new AddProviderCommandHandler(
            Mock.Of<IMediator>(), providerService.Object, Mock.Of<IEventStore>());

        await handler.Handle(new AddProviderCommand { ProviderEntity = provider }, CancellationToken.None);

        Assert.NotEqual("avatar_99", provider.AvatarId);
        Assert.Contains(provider.AvatarId, AvatarCatalog.Ids);
    }
}
