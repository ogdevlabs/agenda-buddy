namespace Booking.Tests.Events;

public class EventsHelperTests
{
    private readonly Mock<IRequestCollection> _mockRequestCollection;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ProviderService> _mockProviderService;
    private readonly Mock<BookingService> _mockBookingService;
    private readonly AppointmentEntity _appointmentEntity;

    public EventsHelperTests()
    {
        _mockRequestCollection = new Mock<IRequestCollection>();
        _mockMediator = new Mock<IMediator>();
        var mockRepositoryProvider = new Mock<IRepository<ProviderEntity>>();
        var mockRepositoryAppointment = new Mock<IRepository<AppointmentEntity>>();
        _mockProviderService = new Mock<ProviderService>(mockRepositoryProvider.Object);
        _mockBookingService = new Mock<BookingService>(mockRepositoryAppointment.Object);
        _appointmentEntity = new AppointmentEntity
        {
            Identifier = "12345",
            EmailProvider = "Provider@email.com",
            EmailCustomer = "Customer@email.com"
        };
        
    }

    [Fact]
    public async Task BookAppointmentEvent_ShouldReturnExpectedResponse()
    {
        // Arrange
        var expectedResponse = "BookingConfirmed";
        _mockRequestCollection
            .Setup(x => x.BookAppointmentRequest(
                _mockMediator.Object,
                _mockProviderService.Object,
                _mockBookingService.Object,
                _appointmentEntity))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await EventsHelper.BookAppointmentEvent(
            _mockRequestCollection.Object,
            _mockMediator.Object,
            _mockProviderService.Object,
            _mockBookingService.Object,
            _appointmentEntity);

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task UpdateAppointmentEvent_ShouldReturnExpectedResponse()
    {
        // Arrange
        var expectedResponse = "UpdateConfirmed";
        _mockRequestCollection
            .Setup(x => x.UpdateAppointmentRequest(
                _mockMediator.Object,
                _mockProviderService.Object,
                _mockBookingService.Object,
                _appointmentEntity.Identifier,
                _appointmentEntity))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await EventsHelper.UpdateAppointmentEvent(
            _mockRequestCollection.Object,
            _mockMediator.Object,
            _mockProviderService.Object,
            _mockBookingService.Object,
            _appointmentEntity);

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task CancelAppointmentEvent_ShouldReturnExpectedResponse()
    {
        // Arrange
        var expectedResponse = "CancellationConfirmed";
        _mockRequestCollection
            .Setup(x => x.CancelAppointmentRequest(
                _mockMediator.Object,
                _mockProviderService.Object,
                _mockBookingService.Object,
                _appointmentEntity.Identifier))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await EventsHelper.CancelAppointmentEvent(
            _mockRequestCollection.Object,
            _mockMediator.Object,
            _mockProviderService.Object,
            _mockBookingService.Object,
            _appointmentEntity);

        // Assert
        Assert.Equal(expectedResponse, result);
    }
}