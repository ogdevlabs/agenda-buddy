using Services.Events;

namespace Services.Tests.Events;

[TestSubject(typeof(EventHelper))]
public class EventHelperTest
{
    [Fact]
    public async Task GetServicesFromProviderEvent_Success()
    {
        // arrange
        var requestCollectionMock = new Mock<IRequestCollection>();
        var mediatorMock = new Mock<IMediator>();
        var repositoryMock = new Mock<IRepository<ProviderEntity>>();
        var providerService = new ProviderService(repositoryMock.Object);
        const string email = "any.valid@email.com";

        mediatorMock.Setup(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()));

        requestCollectionMock
            .Setup(request =>
                request.GetServicesFromProvider(It.IsAny<Mediator>(), It.IsAny<ProviderService>(),
                    It.IsAny<string>()))
            .ReturnsAsync(ServiceEntitiesResponse);

        // act
        var result = await EventHelper.GetServicesFromProviderEvent(requestCollectionMock.Object, mediatorMock.Object,
            providerService, email);

        // assert
        Assert.Equivalent(new List<ServiceEntity>(), result);
    }

    [Fact]
    public async Task AddServicesToProviderEvent_Success()
    {
        // arrange
        var requestCollectionMock = new Mock<IRequestCollection>();
        var mediatorMock = new Mock<IMediator>();
        var repositoryMock = new Mock<IRepository<ProviderEntity>>();
        var providerService = new ProviderService(repositoryMock.Object);
        const string email = "any.valid@email.com";

        mediatorMock.Setup(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()));

        requestCollectionMock
            .Setup(request =>
                request.AddServicesToProvider(It.IsAny<Mediator>(), It.IsAny<ProviderService>(),
                    It.IsAny<List<ServiceEntity>>(), It.IsAny<string>()))
            .ReturnsAsync(ProviderResponse);

        // act
        var result = await EventHelper.AddServicesToProviderEvent(requestCollectionMock.Object, mediatorMock.Object,
            providerService, ServiceEntitiesResponse(), email);

        // assert
        Assert.Equivalent(null, result);
    }

    [Fact]
    public async Task UpdateServicesFromProvider_Success()
    {
        // arrange
        var requestCollectionMock = new Mock<IRequestCollection>();
        var mediatorMock = new Mock<IMediator>();
        var repositoryMock = new Mock<IRepository<ProviderEntity>>();
        var providerService = new ProviderService(repositoryMock.Object);
        const string email = "any.valid@email.com";

        mediatorMock.Setup(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()));

        requestCollectionMock
            .Setup(request =>
                request.UpdateServicesFromProvider(It.IsAny<Mediator>(), providerService,
                    It.IsAny<List<ServiceEntity>>(), It.IsAny<string>()))
            .ReturnsAsync(ProviderResponse);

        // act
        var result = await EventHelper.UpdateServicesFromProviderEvent(requestCollectionMock.Object,
            mediatorMock.Object,
            providerService, ServiceEntitiesResponse(), email);

        // assert
        Assert.Equivalent(null, result);
    }

    private ProviderEntity ProviderResponse()
    {
        return new ProviderEntity()
        {
            Id = new ObjectId("665c96cc7e7cfc229723cd1e"),
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@email.com",
            ServiceEntities =
            [
                new ServiceEntity
                {
                    Name = "A service",
                    Description = "Service description",
                    FeeType = FeeType.Fixed,
                    Fee = 20L,
                    IsActive = true
                },
                new ServiceEntity
                {
                    Name = "Another service",
                    Description = "Service description",
                    FeeType = FeeType.Fixed,
                    Fee = 30L,
                    IsActive = true
                }
            ]
        };
    }

    private List<ServiceEntity> ServiceEntitiesResponse()
    {
        return
        [
            new ServiceEntity
            {
                Name = "A service",
                Description = "Service description",
                FeeType = FeeType.Hourly,
                Fee = 50L,
                IsActive = true
            },

            new ServiceEntity
            {
                Name = "Another service",
                Description = "Service description",
                FeeType = FeeType.Hourly,
                Fee = 60L,
                IsActive = true
            }
        ];
    }
}