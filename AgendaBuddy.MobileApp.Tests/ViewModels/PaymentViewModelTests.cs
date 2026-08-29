using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

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

    // GetAppointmentPaymentQuery answers 404 ("no payment recorded yet") the same way a genuine failure
    // does (both surface as null here) — that is a normal state offering the Pay form, not an error banner.
    [Fact]
    public async Task LoadAsync_ServiceReturnsNull_ShowsPayForm_NotError()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetPaymentAsync("a1", It.IsAny<CancellationToken>()))
               .ReturnsAsync((PaymentEntity?)null);

        var vm = new PaymentViewModel(service.Object) { AppointmentId = "a1" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.False(vm.HasPayment);
        Assert.True(vm.ShowPayForm);
    }

    [Fact]
    public async Task PayAsync_ValidAmount_CreatesPaymentAndClearsPayForm()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetPaymentAsync("a1", It.IsAny<CancellationToken>()))
               .ReturnsAsync((PaymentEntity?)null);
        service.Setup(s => s.CreatePaymentAsync("a1", 42m, null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new PaymentEntity { Amount = 42m, Status = PaymentStatus.Succeeded, StripePaymentIntentId = "pi_123" });

        var vm = new PaymentViewModel(service.Object) { AppointmentId = "a1" };
        await vm.LoadCommand.ExecuteAsync(null);
        vm.PayAmountInput = "42";
        await vm.PayCommand.ExecuteAsync(null);

        Assert.True(vm.HasPayment);
        Assert.False(vm.ShowPayForm);
        Assert.Equal(42, vm.Payment!.Amount);
    }

    [Fact]
    public async Task PayAsync_NonNumericAmount_SetsPayErrorMessage_DoesNotCallService()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetPaymentAsync("a1", It.IsAny<CancellationToken>()))
               .ReturnsAsync((PaymentEntity?)null);

        var vm = new PaymentViewModel(service.Object) { AppointmentId = "a1" };
        await vm.LoadCommand.ExecuteAsync(null);
        vm.PayAmountInput = "not-a-number";
        await vm.PayCommand.ExecuteAsync(null);

        Assert.True(vm.HasPayError);
        service.Verify(s => s.CreatePaymentAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
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
