namespace AgendaBuddy.Booking.Tests.Commands;

public class PayForAppointmentCommandHandlerTest
{
    private static PayForAppointmentCommand MakeCommand() => new()
    {
        Identifier = "abc123",
        ProviderEmail = "provider@example.com",
        CustomerEmail = "customer@example.com",
        Amount = 50m,
        Currency = "usd"
    };

    [Fact]
    public async Task Handle_NoExistingPayment_ChargesAndReturnsOk()
    {
        var charged = new PaymentEntity
        {
            AppointmentIdentifier = "abc123",
            ProviderEmail = "provider@example.com",
            CustomerEmail = "customer@example.com",
            Amount = 50m,
            Currency = "usd"
        };
        var payments = new Mock<IPaymentService>();
        payments.Setup(p => p.GetByAppointmentAsync("abc123")).ReturnsAsync((PaymentEntity?)null);
        payments.Setup(p => p.ChargeAsync(It.Is<PaymentEntity>(pe =>
                pe.AppointmentIdentifier == "abc123" && pe.ProviderEmail == "provider@example.com"
                && pe.CustomerEmail == "customer@example.com" && pe.Amount == 50m && pe.Currency == "usd")))
            .ReturnsAsync(charged);
        var handler = new PayForAppointmentCommandHandler(payments.Object, Mock.Of<IEventStore>());

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(charged, result.Value);
    }

    // A second charge for the same appointment. Thrown, not
    // Result.Fail'd, matching ChangeAppointmentStatusCommandHandler's InvalidOperationException ->
    // 409 precedent, since Result.Fail alone can't distinguish "conflict" from an ordinary failure
    // in AgendaBuddy.Booking.Api's mapping.
    [Fact]
    public async Task Handle_AppointmentAlreadyPaid_ThrowsInvalidOperationException()
    {
        var payments = new Mock<IPaymentService>();
        payments.Setup(p => p.GetByAppointmentAsync("abc123"))
            .ReturnsAsync(new PaymentEntity
            {
                AppointmentIdentifier = "abc123",
                ProviderEmail = "provider@example.com",
                CustomerEmail = "customer@example.com",
                Amount = 50m,
                Currency = "usd"
            });
        var handler = new PayForAppointmentCommandHandler(payments.Object, Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(MakeCommand(), CancellationToken.None));
        payments.Verify(p => p.ChargeAsync(It.IsAny<PaymentEntity>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        var handler = new PayForAppointmentCommandHandler(Mock.Of<IPaymentService>(), Mock.Of<IEventStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }
}
