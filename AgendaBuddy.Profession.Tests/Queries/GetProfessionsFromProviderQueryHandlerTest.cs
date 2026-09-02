namespace AgendaBuddy.Profession.Tests.Queries;

public class GetProfessionsFromProviderQueryHandlerTest
{
    [Fact]
    public async Task Handle_ProviderExists_ReturnsOkWithProfessions()
    {
        var provider = new ProviderEntity { Email = "pat@test.dev", Professions = ["Coaching", "Tutoring"] };
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<MongoDB.Bson.BsonDocument>())).ReturnsAsync(provider);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetProfessionsFromProviderQueryHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetProfessionsFromProviderQuery { Email = "pat@test.dev" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(provider.Professions, result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "GetProfessionsFromProviderQuery")), Times.Once);
    }

    [Fact]
    public async Task Handle_ProviderMissing_ReturnsOkWithEmptyList()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<MongoDB.Bson.BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetProfessionsFromProviderQueryHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetProfessionsFromProviderQuery { Email = "missing@test.dev" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "GetProfessionsFromProviderQuery")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new GetProfessionsFromProviderQueryHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
