namespace AgendaBuddy.Profession.Tests.Commands;

public class AddProfessionsToProviderCommandHandlerTest
{
    private static Mock<IProfessionService> CatalogOf(params string[] names)
    {
        var professionService = new Mock<IProfessionService>();
        professionService.Setup(p => p.GetProfessionCollectionAsync())
            .ReturnsAsync(names.Select(n => new ProfessionEntity { Name = n }).ToList());
        return professionService;
    }

    [Fact]
    public async Task Handle_KnownProfessions_ReturnsOkWithUpdatedList()
    {
        var provider = new ProviderEntity { Email = "pat@test.dev", Professions = ["Coaching"] };
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.AddProfessionsAsync("pat@test.dev", new List<string> { "Tutoring" }))
            .ReturnsAsync(provider);
        var professionService = CatalogOf("Coaching", "Tutoring");
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddProfessionsToProviderCommandHandler(mediator.Object, providerService.Object, professionService.Object, eventStore.Object);

        var result = await handler.Handle(
            new AddProfessionsToProviderCommand { Email = "pat@test.dev", ProfessionNames = ["Tutoring"] },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(provider.Professions, result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "AddProfessionsToProviderCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownProfession_ReturnsFailWithoutTouchingProvider()
    {
        var providerService = new Mock<IProviderService>();
        var professionService = CatalogOf("Coaching");
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddProfessionsToProviderCommandHandler(mediator.Object, providerService.Object, professionService.Object, eventStore.Object);

        var result = await handler.Handle(
            new AddProfessionsToProviderCommand { Email = "pat@test.dev", ProfessionNames = ["NotARealProfession"] },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        providerService.Verify(p => p.AddProfessionsAsync(It.IsAny<string>(), It.IsAny<List<string>>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "AddProfessionsToProviderCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_ProviderMissing_ReturnsFail()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.AddProfessionsAsync(It.IsAny<string>(), It.IsAny<List<string>>()))
            .ReturnsAsync((ProviderEntity)null!);
        var professionService = CatalogOf("Coaching");
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new AddProfessionsToProviderCommandHandler(mediator.Object, providerService.Object, professionService.Object, eventStore.Object);

        var result = await handler.Handle(
            new AddProfessionsToProviderCommand { Email = "missing@test.dev", ProfessionNames = ["Coaching"] },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "AddProfessionsToProviderCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new AddProfessionsToProviderCommandHandler(
            Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IProfessionService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
