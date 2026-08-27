using AgendaBuddy.Library.Entities;
using MobileApp.Infrastructure;
using MobileApp.Services;
using MobileApp.ViewModels;
using Moq;
using Xunit;

namespace MobileApp.Tests.ViewModels;

// ux-review.md finding 1 / PRD Requirement 12 / AC13: the report screen renders
// revenueUnavailableReason when revenueAvailable is false — never a number, never a blank field.
public class ProviderReportViewModelTests
{
    private static ProviderReport UnavailableReport() => new()
    {
        ProviderEmail = "prov@example.com",
        TotalBookings = 10,
        CompletedAppointments = 6,
        CancelledAppointments = 1,
        UniqueCustomers = 4,
        RetentionRate = 0.5,
        RevenueAvailable = false,
        RevenueUnavailableReason = "Appointments do not record which service they were booked for."
    };

    [Fact]
    public async Task LoadAsync_RevenueUnavailable_RendersExactCopyWithReason()
    {
        var service = new Mock<IProviderApiService>();
        service.Setup(s => s.GetReportAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(UnavailableReport());

        var vm = new ProviderReportViewModel(service.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        // The fixture's reason already ends with a period — the exact copy has exactly one, not two.
        Assert.Equal(
            "Revenue isn't available yet — Appointments do not record which service they were booked for.",
            vm.RevenueMessage);
        Assert.DoesNotContain("..", vm.RevenueMessage);
    }

    [Fact]
    public async Task LoadAsync_RevenueUnavailable_NeverRendersANumberOrBlank()
    {
        var service = new Mock<IProviderApiService>();
        service.Setup(s => s.GetReportAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(UnavailableReport());

        var vm = new ProviderReportViewModel(service.Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrWhiteSpace(vm.RevenueMessage));
        Assert.False(decimal.TryParse(vm.RevenueMessage, out _));
        Assert.Contains("Revenue isn't available yet", vm.RevenueMessage);
    }

    [Fact]
    public void RevenueMessage_NoReportLoaded_IsEmpty()
    {
        var vm = new ProviderReportViewModel(new Mock<IProviderApiService>().Object);

        Assert.Equal(string.Empty, vm.RevenueMessage);
    }

    [Fact]
    public async Task LoadAsync_ServiceReturnsNull_SetsErrorMessage()
    {
        var service = new Mock<IProviderApiService>();
        service.Setup(s => s.GetReportAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync((ProviderReport?)null);

        var vm = new ProviderReportViewModel(service.Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.HasReport);
    }

    // ux-review.md finding 2: the gateway's failedService maps to the display-name banner copy.
    [Fact]
    public async Task LoadAsync_GatewayServiceUnavailable_MapsFailedServiceToDisplayName()
    {
        var service = new Mock<IProviderApiService>();
        service.Setup(s => s.GetReportAsync(It.IsAny<CancellationToken>()))
               .ThrowsAsync(new GatewayServiceUnavailableException("provider"));

        var vm = new ProviderReportViewModel(service.Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Providers is unavailable right now. Try again.", vm.ErrorMessage);
    }
}
