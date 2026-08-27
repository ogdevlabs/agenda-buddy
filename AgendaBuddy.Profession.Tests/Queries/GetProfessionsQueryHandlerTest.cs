namespace AgendaBuddy.Profession.Tests.Queries;

public class GetProfessionsQueryHandlerTest
{
    [Fact]
    public async Task Handle_ProfessionsExist_ReturnsOkWithList()
    {
        var professions = new List<ProfessionEntity> { new() { Name = "Coaching" } };
        var professionService = new Mock<IProfessionService>();
        professionService.Setup(p => p.GetProfessionCollectionAsync()).ReturnsAsync(professions);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetProfessionsQueryHandler(mediator.Object, professionService.Object, eventStore.Object);

        var result = await handler.Handle(new GetProfessionsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(professions, result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "GetProfessionsQuery")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoProfessionsSeeded_ReturnsFailAndWritesFailedAudit()
    {
        var professionService = new Mock<IProfessionService>();
        professionService.Setup(p => p.GetProfessionCollectionAsync()).ReturnsAsync([]);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetProfessionsQueryHandler(mediator.Object, professionService.Object, eventStore.Object);

        var result = await handler.Handle(new GetProfessionsQuery(), CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "GetProfessionsQuery")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new GetProfessionsQueryHandler(Mock.Of<IMediator>(), Mock.Of<IProfessionService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
