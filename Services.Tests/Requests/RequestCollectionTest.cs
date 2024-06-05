using System.Collections;
using EventAndCommands.Queries.Services;
using JetBrains.Annotations;
using Library.Entities;
using Library.Repositories;
using Library.Services;
using MediatR;
using MongoDB.Bson;
using Moq;
using Services.Requests;

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
                ServiceEntities = [new ServiceEntity()
                {
                    Name = "Agent",
                    Description = "Special Agent",
                    Fee = 250L,
                    FeeType = FeeType.Fixed,
                    IsActive = true
                }]
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
    }
}