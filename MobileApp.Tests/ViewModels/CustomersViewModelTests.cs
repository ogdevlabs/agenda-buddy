using System.Net.Http;
using MobileApp.Models;
using MobileApp.Services;
using MobileApp.ViewModels;
using Moq;
using Xunit;

namespace MobileApp.Tests.ViewModels;

public class CustomersViewModelTests
{
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

        var vm = new CustomersViewModel(service.Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Customers.Count);
        Assert.Equal("Alice Smith", vm.Customers[0].FullName);
        Assert.False(vm.IsLoading);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task LoadAsync_EmptyResult_IsEmptyIsTrue()
    {
        var service = new Mock<ICustomerApiService>();
        service.Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<CustomerSummary>());

        var vm = new CustomersViewModel(service.Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Customers);
        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasError);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadAsync_NetworkError_SetsErrorMessage()
    {
        var service = new Mock<ICustomerApiService>();
        service.Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network error"));

        var vm = new CustomersViewModel(service.Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal(
            "Could not load customers — check your connection and try again.",
            vm.ErrorMessage);
        Assert.False(vm.IsLoading);
        Assert.False(vm.IsEmpty);
    }
}
