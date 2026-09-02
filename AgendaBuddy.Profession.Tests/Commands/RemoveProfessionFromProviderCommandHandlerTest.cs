namespace AgendaBuddy.Profession.Tests.Commands;

public class RemoveProfessionFromProviderCommandHandlerTest
{
    [Fact]
    public async Task Handle_NoActiveAppointments_ReturnsOkWithUpdatedList()
    {
        var existing = new ProviderEntity { Email = "pat@test.dev", Professions = ["Coaching", "Tutoring"] };
        var afterRemoval = new ProviderEntity { Email = "pat@test.dev", Professions = ["Coaching"] };
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<MongoDB.Bson.BsonDocument>())).ReturnsAsync(existing);
        providerService.Setup(p => p.RemoveProfessionAsync("pat@test.dev", "Tutoring")).ReturnsAsync(afterRemoval);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new RemoveProfessionFromProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new RemoveProfessionFromProviderCommand { Email = "pat@test.dev", ProfessionName = "Tutoring" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(afterRemoval.Professions, result.Value);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "RemoveProfessionFromProviderCommand")), Times.Once);
    }

    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Booked)]
    [InlineData(AppointmentStatus.Confirmed)]
    public async Task Handle_HasActiveAppointment_ReturnsFailAndDoesNotRemove(AppointmentStatus status)
    {
        var existing = new ProviderEntity
        {
            Email = "pat@test.dev",
            Professions = ["Coaching"],
            AppointmentEntities = [new AppointmentEntity { EmailProvider = "pat@test.dev", EmailCustomer = "cust@test.dev", Start = DateTime.UtcNow, End = DateTime.UtcNow.AddHours(1), AppointmentStatus = status }]
        };
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<MongoDB.Bson.BsonDocument>())).ReturnsAsync(existing);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new RemoveProfessionFromProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new RemoveProfessionFromProviderCommand { Email = "pat@test.dev", ProfessionName = "Coaching" },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == RemoveProfessionFromProviderCommandHandler.ActiveAppointmentsErrorMessage);
        providerService.Verify(p => p.RemoveProfessionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "RemoveProfessionFromProviderCommand")), Times.Once);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public async Task Handle_OnlyInactiveAppointments_AllowsRemoval(AppointmentStatus status)
    {
        var existing = new ProviderEntity
        {
            Email = "pat@test.dev",
            Professions = ["Coaching"],
            AppointmentEntities = [new AppointmentEntity { EmailProvider = "pat@test.dev", EmailCustomer = "cust@test.dev", Start = DateTime.UtcNow, End = DateTime.UtcNow.AddHours(1), AppointmentStatus = status }]
        };
        var afterRemoval = new ProviderEntity { Email = "pat@test.dev", Professions = [] };
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<MongoDB.Bson.BsonDocument>())).ReturnsAsync(existing);
        providerService.Setup(p => p.RemoveProfessionAsync("pat@test.dev", "Coaching")).ReturnsAsync(afterRemoval);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new RemoveProfessionFromProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new RemoveProfessionFromProviderCommand { Email = "pat@test.dev", ProfessionName = "Coaching" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ProviderMissing_ReturnsFail()
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.FindProvidersAsync(It.IsAny<MongoDB.Bson.BsonDocument>())).ReturnsAsync((ProviderEntity)null!);
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new RemoveProfessionFromProviderCommandHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(
            new RemoveProfessionFromProviderCommand { Email = "missing@test.dev", ProfessionName = "Coaching" },
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new RemoveProfessionFromProviderCommandHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
