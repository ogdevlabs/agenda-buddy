namespace Provider.Tests.Queries;

public class GetProvidersQueryHandlerTest
{
    private static ProviderEntity Provider(string email) => new()
    {
        FirstName = "Grace",
        LastName = "Hopper",
        Email = email
    };

    [Fact]
    public async Task Handle_ProvidersExist_ReturnsOkWithPagedResponseAndSuccessAudit()
    {
        var providers = new List<ProviderEntity> { Provider("a@example.com"), Provider("b@example.com") };
        var page = PageRequest.Clamp(1, 25);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.GetPagedProvidersAsync(page.Skip, page.PageSize))
            .ReturnsAsync(((IEnumerable<ProviderEntity>)providers, (long)providers.Count));
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetProvidersQueryHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetProvidersQuery { Page = page }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, System.Linq.Enumerable.Count(result.Value.Items));
        Assert.Equal(2, result.Value.TotalCount);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Success" && ev.Type == "GetProvidersQuery")), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoProviders_StillReturnsOkWithEmptyPageButWritesFailedAudit()
    {
        // Preserves Provider/Program.cs's pre-existing behaviour: an empty page is a successful 200, never
        // a Result.Fail -- the "Failed" audit here is an AUDIT distinction only, not a control-flow one.
        var page = PageRequest.Clamp(1, 25);
        var providerService = new Mock<IProviderService>();
        providerService.Setup(p => p.GetPagedProvidersAsync(page.Skip, page.PageSize))
            .ReturnsAsync(((IEnumerable<ProviderEntity>)new List<ProviderEntity>(), 0L));
        var eventStore = new Mock<IEventStore>();
        var mediator = new Mock<IMediator>();
        var handler = new GetProvidersQueryHandler(mediator.Object, providerService.Object, eventStore.Object);

        var result = await handler.Handle(new GetProvidersQuery { Page = page }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        eventStore.Verify(e => e.SaveAsync(It.Is<Event>(ev => ev.Status == "Failed" && ev.Type == "GetProvidersQuery")), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new GetProvidersQueryHandler(Mock.Of<IMediator>(), Mock.Of<IProviderService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
