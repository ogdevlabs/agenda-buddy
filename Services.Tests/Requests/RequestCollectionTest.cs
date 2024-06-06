namespace Services.Tests.Requests;

[TestSubject(typeof(RequestCollection))]
public class RequestCollectionTest
{
    [Fact]
    public async Task GetServicesFromProvider_Returns_Success()
    {
        // arrange
        var mediatorMock = new Mock<IMediator>();
        var repositoryMock = new Mock<IRepository<ProviderEntity>>();

        mediatorMock.Setup(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()));

        repositoryMock.Setup(r => r.Find(It.IsAny<BsonDocument>()))
            .ReturnsAsync(new ProviderEntity()
            {
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane.doe@email.com",
                ServiceEntities =
                [
                    new ServiceEntity()
                    {
                        Name = "Agent",
                        Description = "Special Agent",
                        Fee = 250L,
                        FeeType = FeeType.Fixed,
                        IsActive = true
                    }
                ]
            });

        var providerService = new ProviderService(repositoryMock.Object);
        var requestCollection = new RequestCollection();

        // act
        var result = await requestCollection.GetServicesFromProvider(
            mediatorMock.Object,
            providerService,
            "a.valid@email.com");

        // assert
        Assert.IsAssignableFrom<IEnumerable<ServiceEntity>>(result);
        Assert.Single(result.ToList());
    }

    [Fact]
    public async Task GetServicesFromProvider_Returns_Empty()
    {
        // arrange
        var mediatorMock = new Mock<IMediator>();
        var repositoryMock = new Mock<IRepository<ProviderEntity>>();

        mediatorMock.Setup(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()));

        repositoryMock.Setup(r => r.Find(It.IsAny<BsonDocument>()))
            .ReturnsAsync(new ProviderEntity()
            {
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane.doe@email.com",
                ServiceEntities = []
            });

        var providerService = new ProviderService(repositoryMock.Object);
        var requestCollection = new RequestCollection();

        // act
        var result = await requestCollection.GetServicesFromProvider(
            mediatorMock.Object,
            providerService,
            "a.valid@email.com");

        // assert
        Assert.IsAssignableFrom<IEnumerable<ServiceEntity>>(result);
        Assert.Empty(result.ToList());
    }

    [Fact]
    public async Task AddServicesToProvider_Returns_Success()
    {
        // arrange
        var mediatorMock = new Mock<IMediator>();
        var repositoryMock = new Mock<IRepository<ProviderEntity>>();

        mediatorMock.Setup(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()));

        repositoryMock.Setup(r => r.Find(It.IsAny<BsonDocument>()))
            .ReturnsAsync(ProviderResponse);

        repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(ProviderResponse);

        repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<string>(), 
                It.IsAny<ProviderEntity>()))
            .ReturnsAsync(true);

        var providerServiceMock = new Mock<ProviderService>(repositoryMock.Object);
        var requestCollection = new RequestCollection();
        var serviceCollection = ServiceEntitiesResponse();

        // act
        var result =
            await requestCollection.AddServicesToProvider(mediatorMock.Object, providerServiceMock.Object,
                serviceCollection,
                "jane.doe@email.com");

        // assert
        Assert.IsAssignableFrom<ProviderEntity>(result);
        if (result.ServiceEntities != null) Assert.Equal(4, result.ServiceEntities.ToList().Count);
    }
    
    [Fact]
    public async Task AddServicesToProvider_Returns_False()
    {
        // arrange
        var mediatorMock = new Mock<IMediator>();
        var repositoryMock = new Mock<IRepository<ProviderEntity>>();

        mediatorMock.Setup(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()));

        repositoryMock.Setup(r => r.Find(It.IsAny<BsonDocument>()))
            .ReturnsAsync(ProviderResponse);

        repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(ProviderResponse);

        repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<string>(), 
                It.IsAny<ProviderEntity>()))
            .ReturnsAsync(false);

        var providerServiceMock = new Mock<ProviderService>(repositoryMock.Object);
        var requestCollection = new RequestCollection();
        var serviceCollection = ServiceEntitiesResponse();

        // act
        var result =
            await requestCollection.AddServicesToProvider(mediatorMock.Object, providerServiceMock.Object,
                serviceCollection,
                "jane.doe@email.com");

        // assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateServicesFromProvider_Returns_Success()
    {
        // arrange
        var mediatorMock = new Mock<IMediator>();
        var repositoryMock = new Mock<IRepository<ProviderEntity>>();

        mediatorMock.Setup(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()));

        repositoryMock.Setup(r => r.Find(It.IsAny<BsonDocument>()))
            .ReturnsAsync(ProviderResponse);

        repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(ProviderResponse);

        repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<string>(), 
                It.IsAny<ProviderEntity>()))
            .ReturnsAsync(true);

        var providerServiceMock = new Mock<ProviderService>(repositoryMock.Object);
        var requestCollection = new RequestCollection();
        var serviceCollection = ServiceEntitiesResponse();
        
        // act
        var result =
            await requestCollection.UpdateServicesFromProvider(mediatorMock.Object, providerServiceMock.Object,
                serviceCollection,
                "jane.doe@email.com");

        // assert
        Assert.IsAssignableFrom<ProviderEntity>(result);
        if (result.ServiceEntities != null) Assert.Equal(50L, result.ServiceEntities[0].Fee);
    }
    
    [Fact]
    public async Task UpdateServicesFromProvider_Returns_False()
    {
        // arrange
        var mediatorMock = new Mock<IMediator>();
        var repositoryMock = new Mock<IRepository<ProviderEntity>>();

        mediatorMock.Setup(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()));

        repositoryMock.Setup(r => r.Find(It.IsAny<BsonDocument>()))
            .ReturnsAsync(ProviderResponse);

        repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(ProviderResponse);

        repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<string>(), 
                It.IsAny<ProviderEntity>()))
            .ReturnsAsync(false);

        var providerServiceMock = new Mock<ProviderService>(repositoryMock.Object);
        var requestCollection = new RequestCollection();
        var serviceCollection = ProviderResponse().ServiceEntities;
        
        // act
        var result =
            await requestCollection.UpdateServicesFromProvider(mediatorMock.Object, providerServiceMock.Object,
                serviceCollection,
                "jane.doe@email.com");

        // assert
        Assert.IsAssignableFrom<ProviderEntity>(result);
        if (result.ServiceEntities != null) Assert.Equal(20L, result.ServiceEntities[0].Fee);
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