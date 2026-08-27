namespace AgendaBuddy.Booking.Tests.Queries;

public class GetAppointmentPaymentQueryHandlerTest
{
    [Fact]
    public async Task Handle_PaymentExists_ReturnsOk()
    {
        var payment = new PaymentEntity
        {
            AppointmentIdentifier = "abc123",
            ProviderEmail = "provider@example.com",
            CustomerEmail = "customer@example.com",
            Amount = 50m,
            Currency = "usd"
        };
        var payments = new Mock<IPaymentService>();
        payments.Setup(p => p.GetByAppointmentAsync("abc123")).ReturnsAsync(payment);
        var handler = new GetAppointmentPaymentQueryHandler(payments.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(new GetAppointmentPaymentQuery { Identifier = "abc123" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(payment, result.Value);
    }

    [Fact]
    public async Task Handle_NoPayment_ReturnsFail()
    {
        var payments = new Mock<IPaymentService>();
        payments.Setup(p => p.GetByAppointmentAsync("abc123")).ReturnsAsync((PaymentEntity?)null);
        var handler = new GetAppointmentPaymentQueryHandler(payments.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(new GetAppointmentPaymentQuery { Identifier = "abc123" }, CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new GetAppointmentPaymentQueryHandler(Mock.Of<IPaymentService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
