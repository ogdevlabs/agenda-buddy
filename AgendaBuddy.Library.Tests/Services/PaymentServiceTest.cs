using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

public class PaymentServiceTest
{
    private readonly Mock<IRepository<PaymentEntity>> _repoMock;
    private readonly Mock<IPaymentGateway> _gatewayMock;
    private readonly PaymentService _svc;

    public PaymentServiceTest()
    {
        _repoMock = new Mock<IRepository<PaymentEntity>>();
        _gatewayMock = new Mock<IPaymentGateway>();
        _svc = new PaymentService(_repoMock.Object, _gatewayMock.Object);
    }

    [Fact]
    public async Task RefundAsync_ThrowsKeyNotFound_WhenPaymentMissing()
    {
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync((PaymentEntity?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _svc.RefundAsync("appt-missing"));
    }

    [Fact]
    public async Task RefundAsync_ThrowsInvalidOperation_WhenNotSucceeded()
    {
        var payment = new PaymentEntity("appt-001", "p@ex.com", "c@ex.com", 50m);
        payment.Status = PaymentStatus.Pending;
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(payment);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.RefundAsync("appt-001"));
    }

    [Fact]
    public async Task RefundAsync_SetsRefundedStatus_WhenSuccessful()
    {
        var payment = new PaymentEntity("appt-001", "p@ex.com", "c@ex.com", 50m);
        payment.Status = PaymentStatus.Succeeded;
        payment.StripePaymentIntentId = "pi_test_123";
        payment.Id = ObjectId.GenerateNewId();

        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(payment);
        _gatewayMock.Setup(g => g.RefundPaymentIntentAsync("pi_test_123"))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<PaymentEntity>()))
            .ReturnsAsync(true);

        var result = await _svc.RefundAsync("appt-001");
        Assert.Equal(PaymentStatus.Refunded, result.Status);
    }

    [Fact]
    public async Task ReleaseOrRefundAsync_AuthorizedPayment_CancelsIntentAndPersistsCancelled()
    {
        var payment = new PaymentEntity("appt-001", "p@ex.com", "c@ex.com", 50m)
        {
            Id = ObjectId.GenerateNewId(),
            Status = PaymentStatus.Authorized,
            StripePaymentIntentId = "pi_test_123"
        };
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync(payment);
        _gatewayMock.Setup(g => g.CancelPaymentIntentAsync("pi_test_123")).ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdateAsync(payment.Id.ToString(), payment)).ReturnsAsync(true);

        var result = await _svc.ReleaseOrRefundAsync("appt-001");

        Assert.True(result);
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        _gatewayMock.Verify(g => g.RefundPaymentIntentAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseOrRefundAsync_SucceededPayment_FullyRefunds()
    {
        var payment = new PaymentEntity("appt-001", "p@ex.com", "c@ex.com", 50m)
        {
            Id = ObjectId.GenerateNewId(),
            Status = PaymentStatus.Succeeded,
            StripePaymentIntentId = "pi_test_123"
        };
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync(payment);
        _gatewayMock.Setup(g => g.RefundPaymentIntentAsync("pi_test_123")).ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdateAsync(payment.Id.ToString(), payment)).ReturnsAsync(true);

        var result = await _svc.ReleaseOrRefundAsync("appt-001");

        Assert.True(result);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        _gatewayMock.Verify(g => g.CancelPaymentIntentAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AuthorizeAsync_CreatesManualAuthorizationWithNinetyTenSplit()
    {
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync((PaymentEntity?)null);
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<PaymentEntity>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<PaymentEntity>())).ReturnsAsync(true);
        _gatewayMock.Setup(g => g.AuthorizeAsync(It.Is<PaymentAuthorizationRequest>(request =>
                request.AmountMinor == 10001
                && request.ApplicationFeeMinor == 1000
                && request.ConnectedAccountId == "acct_123"
                && request.CustomerId == "cus_123"
                && request.PaymentMethodId == "pm_123")))
            .ReturnsAsync(new PaymentAuthorizationResult("pi_123", "requires_capture"));
        var appointment = new AppointmentEntity
        {
            Identifier = "appt-001",
            EmailProvider = "p@ex.com",
            EmailCustomer = "c@ex.com",
            ServiceName = "Consultation",
            Start = DateTime.UtcNow.AddDays(2),
            PaymentAmountMinor = 10001,
            PaymentCurrency = "usd"
        };
        var customer = new CustomerEntity
        {
            StripeCustomerId = "cus_123",
            StripeDefaultPaymentMethodId = "pm_123"
        };
        var provider = new ProviderEntity
        {
            StripeConnectedAccountId = "acct_123",
            StripeChargesEnabled = true,
            StripePayoutsEnabled = true
        };

        var result = await _svc.AuthorizeAsync(appointment, customer, provider);

        Assert.Equal(PaymentStatus.Authorized, result.Status);
        Assert.Equal(1000, result.ApplicationFeeMinor);
        Assert.Equal(9001, result.ProviderAmountMinor);
        Assert.Equal(1000, result.FeeBasisPoints);
    }

    [Fact]
    public async Task CaptureAsync_AuthorizedPayment_CapturesAndPersistsSucceeded()
    {
        var payment = new PaymentEntity("appt-001", "p@ex.com", "c@ex.com", 50m)
        {
            Id = ObjectId.GenerateNewId(),
            Status = PaymentStatus.Authorized,
            StripePaymentIntentId = "pi_123"
        };
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync(payment);
        _gatewayMock.Setup(g => g.CaptureAsync("pi_123", "appt-001")).ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdateAsync(payment.Id.ToString(), payment)).ReturnsAsync(true);

        var result = await _svc.CaptureAsync("appt-001");

        Assert.True(result);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.NotNull(payment.CapturedAt);
    }

    [Fact]
    public async Task CaptureAsync_ExpiredAuthorization_IsCancelledWithoutCapture()
    {
        var payment = new PaymentEntity("appt-001", "p@ex.com", "c@ex.com", 50m)
        {
            Id = ObjectId.GenerateNewId(),
            Status = PaymentStatus.Authorized,
            StripePaymentIntentId = "pi_123",
            AuthorizationExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        _repoMock.Setup(r => r.FindOneAsync(It.IsAny<BsonDocument>())).ReturnsAsync(payment);
        _repoMock.Setup(r => r.UpdateAsync(payment.Id.ToString(), payment)).ReturnsAsync(true);

        var result = await _svc.CaptureAsync("appt-001");

        Assert.False(result);
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        _gatewayMock.Verify(g => g.CaptureAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void PaymentEntity_DefaultStatus_IsPending()
    {
        var p = new PaymentEntity();
        Assert.Equal(PaymentStatus.Pending, p.Status);
    }

    [Fact]
    public void PaymentEntity_DefaultCurrency_IsUsd()
    {
        var p = new PaymentEntity("appt", "p@ex.com", "c@ex.com", 100m);
        Assert.Equal("usd", p.Currency);
    }
}
