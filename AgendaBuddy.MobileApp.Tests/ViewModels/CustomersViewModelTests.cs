using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class CustomersViewModelTests
{
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
    public async Task LoadAsync_Success_SetsCustomers()
    {
        var customers = new List<CustomerSummary>
        {
            new() { Id = "1", FullName = "Alice Smith", Email = "alice@example.com" },
            new() { Id = "2", FullName = "Bob Jones", Email = "bob@example.com" }
        };

        var service = new Mock<ICustomerApiService>();
        service.Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(customers);

        var vm = new CustomersViewModel(service.Object, CreateMockSession().Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Customers.Count);
        Assert.Equal("Alice Smith", vm.Customers[0].FullName);
        Assert.False(vm.IsLoading);
        Assert.False(vm.HasError);
    }

    // A genuine zero-result success surfaces the empty state (IsEmpty), never
    // fabricated seed contacts.
    [Fact]
    public async Task LoadAsync_EmptyResult_SetsIsEmptyTrue_NoFabricatedData()
    {
        var service = new Mock<ICustomerApiService>();
        service.Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<CustomerSummary>());

        var vm = new CustomersViewModel(service.Object, CreateMockSession().Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Customers);
        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasError);
        Assert.False(vm.IsLoading);
    }

    // A genuine failure surfaces the error banner (HasError + a real ErrorMessage),
    // never fabricated seed contacts.
    [Fact]
    public async Task LoadAsync_NetworkError_SetsHasErrorTrueWithRealMessage_NoFabricatedData()
    {
        var service = new Mock<ICustomerApiService>();
        service.Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network error"));

        var vm = new CustomersViewModel(service.Object, CreateMockSession().Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Customers);
        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadAsync_CustomerRole_SetsProviderPageTitle()
    {
        var service = new Mock<ICustomerApiService>();
        service.Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<CustomerSummary>());

        var vm = new CustomersViewModel(service.Object, CreateMockSession("alex.chen@agendabuddy.dev", "Customer").Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Providers", vm.PageTitle);
        Assert.Equal("Search by name or service...", vm.SearchPlaceholder);
    }

    [Fact]
    public async Task LoadAsync_ProviderRole_SetsCustomerPageTitle()
    {
        var service = new Mock<ICustomerApiService>();
        service.Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<CustomerSummary>());

        var vm = new CustomersViewModel(service.Object, CreateMockSession().Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Customers", vm.PageTitle);
        Assert.Equal("Search customers...", vm.SearchPlaceholder);
    }
}
