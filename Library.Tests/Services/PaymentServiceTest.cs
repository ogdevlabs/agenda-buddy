using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Library.Entities;
using Library.Repositories;
using Library.Services;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace Library.Tests.Services;

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
    public async Task ChargeAsync_SetsSucceededStatus_WhenGatewayConfirms()
    {
        _gatewayMock.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("pi_test_123");
        _gatewayMock.Setup(g => g.ConfirmPaymentIntentAsync("pi_test_123"))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<PaymentEntity>()))
            .Returns(Task.CompletedTask);

        var payment = new PaymentEntity("appt-001", "p@ex.com", "c@ex.com", 50m);
        var result = await _svc.ChargeAsync(payment);

        Assert.Equal(PaymentStatus.Succeeded, result.Status);
        Assert.Equal("pi_test_123", result.StripePaymentIntentId);
        Assert.NotEqual(ObjectId.Empty, result.Id);
    }

    [Fact]
    public async Task ChargeAsync_SetsFailedStatus_WhenGatewayRejects()
    {
        _gatewayMock.Setup(g => g.CreatePaymentIntentAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("pi_fail_456");
        _gatewayMock.Setup(g => g.ConfirmPaymentIntentAsync("pi_fail_456"))
            .ReturnsAsync(false);
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<PaymentEntity>()))
            .Returns(Task.CompletedTask);

        var payment = new PaymentEntity("appt-002", "p@ex.com", "c@ex.com", 75m);
        var result = await _svc.ChargeAsync(payment);

        Assert.Equal(PaymentStatus.Failed, result.Status);
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
