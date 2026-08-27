namespace Profession.Tests.Queries;

public class GetProfessionByNameQueryHandlerTest
{
    [Fact]
    public async Task Handle_ProfessionExists_ReturnsOkWithProfession()
    {
        var profession = new ProfessionEntity { Name = "Coaching" };
        var professionService = new Mock<IProfessionService>();
        professionService.Setup(p => p.GetProfessionAsync("Coaching")).ReturnsAsync(profession);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetProfessionByNameQueryHandler(mediator.Object, professionService.Object, eventStore.Object);

        var result = await handler.Handle(new GetProfessionByNameQuery { Name = "Coaching" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(profession, result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "GetProfessionByNameQuery")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProfession_ReturnsFailAndWritesFailedAudit()
    {
        var professionService = new Mock<IProfessionService>();
        professionService.Setup(p => p.GetProfessionAsync("no-such-profession")).ReturnsAsync((ProfessionEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetProfessionByNameQueryHandler(mediator.Object, professionService.Object, eventStore.Object);

        var result = await handler.Handle(new GetProfessionByNameQuery { Name = "no-such-profession" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "GetProfessionByNameQuery")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new GetProfessionByNameQueryHandler(Mock.Of<IMediator>(), Mock.Of<IProfessionService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
