using System.Collections.Generic;
using System.Threading.Tasks;
using Library.Entities;
using Library.Repositories;
using Library.Services;
using MediatR;
using Profession.Events;
using Profession.Requests;

namespace Profession.Tests.Events;

[TestSubject(typeof(EventsHelper))]
public class EventsHelperTest
{
    private readonly Mock<IRequestCollection> _mockRequestCollection;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ProfessionService> _mockProfessionService;
    private readonly ProfessionEntity _professionEntity;

    public EventsHelperTest()
    {
        _mockRequestCollection = new Mock<IRequestCollection>();
        _mockMediator = new Mock<IMediator>();
        var mockRepositoryProfession = new Mock<IRepository<ProfessionEntity>>();
        _mockProfessionService = new Mock<ProfessionService>(mockRepositoryProfession.Object);
        _professionEntity = new ProfessionEntity
        {
            Id = default,
            Name = "AnyName",
            Description = "AnyDescription"
        };
    }

    [Fact]
    public async Task AddProfessionEvent_ReturnSuccess()
    {
        // Arrange
        var expectedResponse = new ProfessionEntity
        {
            Name = "A profession name",
            Description = "A profession description"
        };
        _mockRequestCollection.Setup(rc =>
                rc.AddProfessionRequest(It.IsAny<IMediator>(), It.IsAny<ProfessionService>(),
                    It.IsAny<ProfessionEntity>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await EventsHelper.AddProfessionEvent(_mockRequestCollection.Object, _mockMediator.Object,
            _mockProfessionService.Object, _professionEntity);
        
        // Assert
        Assert.Equal(expectedResponse, result);
    }
    
    [Fact]
    public async Task GetProfessionByNameEvent_ReturnSuccess()
    {
        // Arrange
        var expectedResponse = new ProfessionEntity
        {
            Name = "A profession name",
            Description = "A profession description"
        };
        _mockRequestCollection.Setup(rc =>
                rc.GetProfessionByNameRequest(It.IsAny<IMediator>(), It.IsAny<ProfessionService>(),
                    It.IsAny<string>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await EventsHelper.GetProfessionByNameEvent(_mockRequestCollection.Object, _mockMediator.Object,
            _mockProfessionService.Object, "AnyName");
        
        // Assert
        Assert.Equal(expectedResponse, result);
    }
    
    [Fact]
    public async Task GetProfessionsEvent_ReturnSuccess()
    {
        // Arrange
        var expectedResponse = new List<ProfessionEntity>
        {
            new ProfessionEntity
            {
                Name = "A profession name",
                Description = "A profession description"
            }
        };
        _mockRequestCollection.Setup(rc =>
                rc.GetProfessionsRequest(It.IsAny<IMediator>(), It.IsAny<ProfessionService>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await EventsHelper.GetAllProfessionsEvent(_mockRequestCollection.Object, _mockMediator.Object,
            _mockProfessionService.Object);
        
        // Assert
        Assert.Equal(expectedResponse, result);
    }
}