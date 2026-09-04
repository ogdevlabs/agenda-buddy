namespace AgendaBuddy.Provider.Tests.Commands;

public class SetProviderWorkHoursCommandHandlerTest
{
    private const string ProviderEmail = "provider@example.com";

    private static ProviderEntity Provider(int? startHour = null, int? endHour = null) => new()
    {
        FirstName = "Grace",
        LastName = "Hopper",
        Email = ProviderEmail,
        WorkDayStartHour = startHour,
        WorkDayEndHour = endHour
    };

    private static (SetProviderWorkHoursCommandHandler Handler, Mock<IProviderService> Service, Mock<IEventStore> Audit)
        Build(ProviderEntity? updated)
    {
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.SetWorkHoursAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                       .ReturnsAsync(updated);

        var eventStore = new Mock<IEventStore>();

        return (new SetProviderWorkHoursCommandHandler(providerService.Object, eventStore.Object),
                providerService,
                eventStore);
    }

    [Fact]
    public async Task Handle_ValidWindow_WritesItWithATargetedUpdateAndAuditsSuccess()
    {
        var (handler, service, audit) = Build(Provider(startHour: 7, endHour: 15));

        var result = await handler.Handle(
            new SetProviderWorkHoursCommand { Email = ProviderEmail, StartHour = 7, EndHour = 15 },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.WorkDayStartHour);
        Assert.Equal(15, result.Value.WorkDayEndHour);
        service.Verify(p => p.SetWorkHoursAsync(ProviderEmail, 7, 15), Times.Once);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Success" && ev.Type == "SetProviderWorkHoursCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_NoSuchProvider_ReturnsFailAndAuditsTheFailure()
    {
        var (handler, service, audit) = Build(updated: null);

        var result = await handler.Handle(
            new SetProviderWorkHoursCommand { Email = "missing@example.com", StartHour = 8, EndHour = 17 },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        service.Verify(p => p.SetWorkHoursAsync("missing@example.com", 8, 17), Times.Once);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Failed" && ev.Type == "SetProviderWorkHoursCommand")), Times.Once);
    }

    [Theory]
    [InlineData(17, 8)]
    [InlineData(9, 9)]
    [InlineData(-1, 17)]
    [InlineData(24, 24)]
    [InlineData(8, 0)]
    [InlineData(8, 25)]
    public async Task Handle_UnusableWindow_IsRefusedWithoutTouchingTheDocument(int startHour, int endHour)
    {
        var (handler, service, audit) = Build(Provider());

        var result = await handler.Handle(
            new SetProviderWorkHoursCommand { Email = ProviderEmail, StartHour = startHour, EndHour = endHour },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        service.Verify(p => p.SetWorkHoursAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        audit.Verify(e => e.SaveAsync(It.Is<Event>(
            ev => ev.Status == "Failed" && ev.Type == "SetProviderWorkHoursCommand")), Times.Once);
    }

    [Fact]
    public async Task Handle_MidnightClose_IsAccepted()
    {
        var (handler, service, _) = Build(Provider(startHour: 20, endHour: 24));

        var result = await handler.Handle(
            new SetProviderWorkHoursCommand { Email = ProviderEmail, StartHour = 20, EndHour = 24 },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        service.Verify(p => p.SetWorkHoursAsync(ProviderEmail, 20, 24), Times.Once);
    }
}
