namespace Customer.Tests.Events;

[TestSubject(typeof(EventsHelper))]
public class EventsHelperTest
{
    private readonly Mock<IRequestCollection> _mockRequestCollection;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<CustomerService> _mockCustomerService;
    private readonly CustomerEntity _customerEntity;

    public EventsHelperTest()
    {
        _mockRequestCollection = new Mock<IRequestCollection>();
        _mockMediator = new Mock<IMediator>();
        var mockRepositoryCustomer = new Mock<IRepository<CustomerEntity>>();
        _mockCustomerService = new Mock<CustomerService>(mockRepositoryCustomer.Object);
        _customerEntity = new CustomerEntity { };
    }

    [Fact]
    public async Task AddCustomerEvent_ShouldAddCustomer()
    {
        // Arrange
        var expectedResponse = "Created";
        _mockRequestCollection
            .Setup(x => x.AddCustomerRequest(
                _mockMediator.Object,
                _mockCustomerService.Object,
                _customerEntity))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await EventsHelper.AddCustomerEvent(
            _mockRequestCollection.Object,
            _mockMediator.Object,
            _mockCustomerService.Object,
            _customerEntity);

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task UpdateCustomerEvent_ShouldReturnUpdatedCustomer()
    {
        // Arrange
        var expectedResponse = "Updated";
        _mockRequestCollection
            .Setup(x => x.UpdateCustomerRequest(
                It.IsAny<string>(),
                _mockMediator.Object,
                _mockCustomerService.Object,
                _customerEntity))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await EventsHelper.UpdateCustomerEvent(It.IsAny<string>(), _mockRequestCollection.Object,
            _mockMediator.Object, _mockCustomerService.Object, _customerEntity);

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task GetCustomersEvent_ShouldReturnCustomers()
    {
        // Arrange
        var expectedResponse = new List<CustomerEntity>
        {
            new() { },
            new() { }
        };
        _mockRequestCollection
            .Setup(rc => rc.GetCustomersRequest(It.IsAny<IMediator>(), It.IsAny<CustomerService>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await EventsHelper.GetCustomersEvent(_mockRequestCollection.Object, _mockMediator.Object,
            _mockCustomerService.Object);

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task GetCustomerByEmail_ShouldReturnCustomer()
    {
        // Arrange
        var expectedResponse = new CustomerEntity { };

        _mockRequestCollection
            .Setup(rc => rc.GetCustomerByEmail(It.IsAny<IMediator>(), It.IsAny<CustomerService>(), It.IsAny<string>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await EventsHelper.GetCustomerByEmailEvent(_mockRequestCollection.Object, _mockMediator.Object,
            _mockCustomerService.Object, "Any@email.com");

        // Assert
        Assert.Equal(expectedResponse, result);
    }
}