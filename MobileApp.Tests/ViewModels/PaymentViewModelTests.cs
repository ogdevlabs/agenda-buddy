using AgendaBuddy.Library.Entities;
using MobileApp.Infrastructure;
using MobileApp.Services;
using MobileApp.ViewModels;
using Moq;
using Xunit;

namespace MobileApp.Tests.ViewModels;

// ux-review.md finding 1 / PRD Requirement 12 / AC13: the payment screen's copy does not claim a
// local_-prefixed payment has been charged — never "Paid".
public class PaymentViewModelTests
{
    private static PaymentEntity LocalIntentPayment(PaymentStatus status = PaymentStatus.Succeeded) => new(
        "a1", "prov@example.com", "alice@example.com", 50m)
    {
        StripePaymentIntentId = "local_64f0c2f1a1b2c3d4",
        Status = status
    };

    private static PaymentEntity RealChargedPayment() => new(
        "a1", "prov@example.com", "alice@example.com", 50m)
    {
        StripePaymentIntentId = "pi_3Nabc123",
        Status = PaymentStatus.Succeeded
    };

    [Fact]
    public async Task LoadAsync_LocalIntentSucceeded_RendersRecordedNotChargedCopy_NeverPaid()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetPaymentAsync("a1", It.IsAny<CancellationToken>()))
               .ReturnsAsync(LocalIntentPayment());

        var vm = new PaymentViewModel(service.Object) { AppointmentId = "a1" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Payment recorded (not yet charged)", vm.StatusMessage);
        Assert.DoesNotContain("Paid", vm.StatusMessage);
        Assert.True(vm.IsNonCharging);
    }

    [Fact]
    public async Task LoadAsync_LocalIntentAnyStatus_IsAlwaysNonCharging()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetPaymentAsync("a1", It.IsAny<CancellationToken>()))
               .ReturnsAsync(LocalIntentPayment(PaymentStatus.Pending));

        var vm = new PaymentViewModel(service.Object) { AppointmentId = "a1" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Payment recorded (not yet charged)", vm.StatusMessage);
    }

    [Fact]
    public async Task LoadAsync_RealStripeIntentSucceeded_RendersPaid()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetPaymentAsync("a1", It.IsAny<CancellationToken>()))
               .ReturnsAsync(RealChargedPayment());

        var vm = new PaymentViewModel(service.Object) { AppointmentId = "a1" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Paid", vm.StatusMessage);
        Assert.False(vm.IsNonCharging);
    }

    [Fact]
    public async Task LoadAsync_ServiceReturnsNull_SetsErrorMessage()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetPaymentAsync("a1", It.IsAny<CancellationToken>()))
               .ReturnsAsync((PaymentEntity?)null);

        var vm = new PaymentViewModel(service.Object) { AppointmentId = "a1" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.HasPayment);
    }

    [Fact]
    public async Task LoadAsync_GatewayServiceUnavailable_MapsFailedServiceToDisplayName()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetPaymentAsync("a1", It.IsAny<CancellationToken>()))
               .ThrowsAsync(new GatewayServiceUnavailableException("booking"));

        var vm = new PaymentViewModel(service.Object) { AppointmentId = "a1" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Booking is unavailable right now. Try again.", vm.ErrorMessage);
    }
}
