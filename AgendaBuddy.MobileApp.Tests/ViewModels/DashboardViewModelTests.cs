using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class DashboardViewModelTests
{
    /// <summary>
    /// The dashboard names the signed-in user beside the greeting, so it takes the singleton that resolves
    /// that name. Given no profile, the name is empty and the greeting stands alone.
    /// </summary>
    private static BrandHeaderViewModel SignedInUser(
        Mock<IUserSessionService> session, string? firstName = null, string? lastName = null)
    {
        var providerApi = new Mock<IProviderApiService>();
        var customerApi = new Mock<ICustomerApiService>();

        if (firstName is not null)
        {
            var profile = new ProfileInfo { FirstName = firstName, LastName = lastName ?? string.Empty };
            providerApi.Setup(p => p.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(profile);
            customerApi.Setup(c => c.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(profile);
        }

        return new BrandHeaderViewModel(session.Object, providerApi.Object, customerApi.Object,
            new NotificationBadgeViewModel(new Mock<INotificationApiService>().Object));
    }

    private static Mock<IUserSessionService> CreateMockSession(string email = "sarah.mitchell@agendabuddy.dev", string role = "Provider")
    {
        var session = new Mock<IUserSessionService>();
        session.Setup(s => s.Email).Returns(email);
        session.Setup(s => s.Role).Returns(role);
        session.Setup(s => s.IsProvider).Returns(role == "Provider");
        session.Setup(s => s.IsCustomer).Returns(role == "Customer");
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session;
    }

    [Fact]
    public async Task LoadAsync_Success_SetsAppointmentsAndClearsError()
    {
        var appointments = new List<AppointmentSummary>
        {
            new() { Id = "a1", CustomerEmail = "alice@example.com", Status = AppointmentStatus.Requested }
        };

        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetUpcomingAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(appointments);

        var session = CreateMockSession();
        var vm = new DashboardViewModel(service.Object, session.Object, SignedInUser(session));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Single(vm.Appointments);
        Assert.Equal(string.Empty, vm.ErrorMessage);
        Assert.False(vm.HasError);
    }

    // A genuine failure surfaces the error banner (HasError + a real ErrorMessage),
    // never fabricated SeedDataProvider appointments.
    [Fact]
    public async Task LoadAsync_NetworkError_SetsHasErrorTrueWithRealMessage_NoFabricatedData()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetUpcomingAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network error"));

        var session = CreateMockSession();
        var vm = new DashboardViewModel(service.Object, session.Object, SignedInUser(session));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Appointments);
        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
        Assert.False(vm.IsEmpty);
    }

    // A genuine zero-result success surfaces the empty state (IsEmpty), never
    // fabricated SeedDataProvider appointments.
    [Fact]
    public async Task LoadAsync_EmptyResult_SetsIsEmptyTrue_NoFabricatedData()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetUpcomingAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<AppointmentSummary>());

        var session = CreateMockSession();
        var vm = new DashboardViewModel(service.Object, session.Object, SignedInUser(session));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Appointments);
        Assert.False(vm.HasError);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task LoadAsync_CustomerRole_SetsDisplayNameToProviderName()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetUpcomingAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<AppointmentSummary>());

        var session = CreateMockSession("alex.chen@agendabuddy.dev", "Customer");
        var vm = new DashboardViewModel(service.Object, session.Object, SignedInUser(session));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.All(vm.Appointments, a => Assert.False(string.IsNullOrEmpty(a.DisplayName)));
        Assert.All(vm.Appointments, a => Assert.NotEqual("Alex Chen", a.DisplayName));
    }

    [Fact]
    public async Task LoadAsync_ProviderRole_SetsDisplayNameToCustomerName()
    {
        var service = new Mock<IBookingApiService>();
        service.Setup(s => s.GetUpcomingAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<AppointmentSummary>());

        var session = CreateMockSession();
        var vm = new DashboardViewModel(service.Object, session.Object, SignedInUser(session));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.All(vm.Appointments, a => Assert.Equal(a.CustomerName, a.DisplayName));
    }
}

public class DashboardGreetingNameTests
{
    private static Mock<IUserSessionService> Session()
    {
        var session = new Mock<IUserSessionService>();
        session.Setup(s => s.Email).Returns("pat@example.com");
        session.Setup(s => s.Role).Returns("Provider");
        session.Setup(s => s.IsProvider).Returns(true);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session;
    }

    private static DashboardViewModel Build(string? firstName, string? lastName = null)
    {
        var session = Session();
        var providerApi = new Mock<IProviderApiService>();
        if (firstName is not null)
        {
            providerApi.Setup(p => p.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new ProfileInfo { FirstName = firstName, LastName = lastName ?? string.Empty });
        }

        var booking = new Mock<IBookingApiService>();
        booking.Setup(b => b.GetUpcomingAppointmentsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<AppointmentSummary>());

        return new DashboardViewModel(
            booking.Object,
            session.Object,
            new BrandHeaderViewModel(session.Object, providerApi.Object, new Mock<ICustomerApiService>().Object,
                new NotificationBadgeViewModel(new Mock<INotificationApiService>().Object)));
    }

    [Fact]
    public async Task TheGreetingIsFollowedByTheUsersName()
    {
        var vm = Build("Pat", "Coach");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Pat Coach", vm.UserDisplayName);
        Assert.Equal(", Pat Coach", vm.GreetingNameSuffix);
        Assert.True(vm.HasUserDisplayName);
    }

    [Fact]
    public async Task WithNoProfileTheGreetingFallsBackToTheEmailRatherThanNothing()
    {
        var vm = Build(firstName: null);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("pat@example.com", vm.UserDisplayName);
        Assert.Equal(", pat@example.com", vm.GreetingNameSuffix);
    }

    [Fact]
    public void BeforeAnythingLoadsTheGreetingCarriesNoPunctuation()
    {
        var vm = Build("Pat", "Coach");

        Assert.Equal(string.Empty, vm.GreetingNameSuffix);
        Assert.False(vm.HasUserDisplayName);
    }
}
