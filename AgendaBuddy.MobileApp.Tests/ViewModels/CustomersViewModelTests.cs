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

    private static Mock<IProviderApiService> CreateMockProviderApi(List<CustomerSummary>? providers = null)
    {
        var providerApi = new Mock<IProviderApiService>();
        providerApi.Setup(p => p.GetProvidersAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(providers ?? new List<CustomerSummary>());
        return providerApi;
    }

    private static Mock<ICustomerApiService> CreateMockCustomerApi(List<CustomerSummary>? customers = null)
    {
        var customerApi = new Mock<ICustomerApiService>();
        customerApi.Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(customers ?? new List<CustomerSummary>());
        customerApi.Setup(s => s.GetSubscriptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<string>());
        return customerApi;
    }

    [Fact]
    public async Task LoadAsync_ProviderRole_Success_SetsCustomers()
    {
        var customers = new List<CustomerSummary>
        {
            new() { Id = "1", FullName = "Alice Smith", Email = "alice@example.com" },
            new() { Id = "2", FullName = "Bob Jones", Email = "bob@example.com" }
        };

        var service = CreateMockCustomerApi(customers);

        var vm = new CustomersViewModel(service.Object, CreateMockProviderApi().Object, CreateMockSession().Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Customers.Count);
        Assert.Equal("Alice Smith", vm.Customers[0].FullName);
        Assert.False(vm.IsLoading);
        Assert.False(vm.HasError);
        service.Verify(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Coach")]
    [InlineData("coach@example.com")]
    [InlineData("Fitness")]
    public async Task LoadAsync_CustomerRole_SearchText_MatchesByNameEmailOrProfession(string searchText)
    {
        var providers = new List<CustomerSummary>
        {
            new() { Email = "coach@example.com", FullName = "Pat Coach", Professions = ["Fitness"] },
            new() { Email = "tutor@example.com", FullName = "Sam Tutor", Professions = ["Tutoring"] }
        };

        var vm = new CustomersViewModel(
            CreateMockCustomerApi().Object,
            CreateMockProviderApi(providers).Object,
            CreateMockSession(role: "Customer").Object);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SearchText = searchText;

        Assert.Single(vm.Customers);
        Assert.Equal("coach@example.com", vm.Customers[0].Email);
    }

    // A genuine zero-result success surfaces the empty state (IsEmpty), never
    // fabricated seed contacts.
    [Fact]
    public async Task LoadAsync_EmptyResult_SetsIsEmptyTrue_NoFabricatedData()
    {
        var service = CreateMockCustomerApi();

        var vm = new CustomersViewModel(service.Object, CreateMockProviderApi().Object, CreateMockSession().Object);
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

        var vm = new CustomersViewModel(service.Object, CreateMockProviderApi().Object, CreateMockSession().Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Customers);
        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
        Assert.False(vm.IsLoading);
    }

    // Bug fix: a Customer used to trigger GET /api/v1/customers (Provider-role-gated, a 403 in practice)
    // unconditionally. A Customer must browse the provider directory instead.
    [Fact]
    public async Task LoadAsync_CustomerRole_CallsProviderDirectory_NotCustomerList()
    {
        var providers = new List<CustomerSummary> { new() { Email = "prov@example.com", FullName = "Prov Ider", IsProvider = true } };
        var customerApi = CreateMockCustomerApi();
        var providerApi = CreateMockProviderApi(providers);

        var vm = new CustomersViewModel(customerApi.Object, providerApi.Object, CreateMockSession("alex.chen@agendabuddy.dev", "Customer").Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Providers", vm.PageTitle);
        Assert.Equal("Search by name, email or service...", vm.SearchPlaceholder);
        Assert.Single(vm.Customers);
        customerApi.Verify(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()), Times.Never);
        providerApi.Verify(p => p.GetProvidersAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadAsync_CustomerRole_MarksSubscribedProviders()
    {
        var providers = new List<CustomerSummary> { new() { Email = "prov@example.com", FullName = "Prov Ider", IsProvider = true } };
        var customerApi = CreateMockCustomerApi();
        customerApi.Setup(s => s.GetSubscriptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<string> { "prov@example.com" });
        var providerApi = CreateMockProviderApi(providers);

        var vm = new CustomersViewModel(customerApi.Object, providerApi.Object, CreateMockSession("alex.chen@agendabuddy.dev", "Customer").Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.Customers[0].IsSubscribed);
    }

    [Fact]
    public async Task LoadAsync_ProviderRole_SetsCustomerPageTitle()
    {
        var service = CreateMockCustomerApi();

        var vm = new CustomersViewModel(service.Object, CreateMockProviderApi().Object, CreateMockSession().Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Customers", vm.PageTitle);
        Assert.Equal("Search customers...", vm.SearchPlaceholder);
    }

    private static CustomersViewModel CustomerFacing(List<CustomerSummary> providers) =>
        new(CreateMockCustomerApi().Object,
            CreateMockProviderApi(providers).Object,
            CreateMockSession(role: "Customer").Object);

    private static List<CustomerSummary> TwoProfessions() =>
    [
        new() { Email = "coach@example.com", FullName = "Pat Coach", Professions = ["Fitness"] },
        new() { Email = "tutor@example.com", FullName = "Sam Tutor", Professions = ["Tutoring"] },
        new() { Email = "both@example.com",  FullName = "Max Both",  Professions = ["Fitness", "Tutoring"] },
    ];

    // Profession is the FIRST filter layer: picking one narrows the provider list before any text search.
    [Fact]
    public async Task LoadAsync_CustomerRole_CollectsProfessionsFromTheLoadedProviders()
    {
        var vm = CustomerFacing(TwoProfessions());
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Fitness", "Tutoring"], vm.AvailableProfessions);
        Assert.True(vm.HasProfessionFilter);
        Assert.Null(vm.SelectedProfession);
        Assert.Equal(3, vm.Customers.Count);
    }

    [Fact]
    public async Task SelectingAProfessionNarrowsToProvidersOfferingIt()
    {
        var vm = CustomerFacing(TwoProfessions());
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectProfessionCommand.Execute("Fitness");

        Assert.Equal(["coach@example.com", "both@example.com"], vm.Customers.Select(c => c.Email));
        Assert.True(vm.HasSelectedProfession);
    }

    // Tapping the active chip clears it, so "all" is reachable without a separate control.
    [Fact]
    public async Task SelectingTheAlreadySelectedProfessionClearsIt()
    {
        var vm = CustomerFacing(TwoProfessions());
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectProfessionCommand.Execute("Fitness");
        vm.SelectProfessionCommand.Execute("Fitness");

        Assert.Null(vm.SelectedProfession);
        Assert.Equal(3, vm.Customers.Count);
    }

    // The two layers compose: text searches WITHIN the chosen profession rather than replacing it.
    [Fact]
    public async Task ProfessionAndSearchTextApplyTogether()
    {
        var vm = CustomerFacing(TwoProfessions());
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectProfessionCommand.Execute("Fitness");
        vm.SearchText = "Max";

        Assert.Equal(["both@example.com"], vm.Customers.Select(c => c.Email));

        // ...and a term that only matches OUTSIDE the profession finds nothing, proving the scope holds.
        vm.SearchText = "Sam";
        Assert.Empty(vm.Customers);
    }

    // A filter offering one option filters nothing, so it is not shown.
    [Fact]
    public async Task ASingleProfessionDoesNotOfferAFilter()
    {
        var vm = CustomerFacing([new() { Email = "a@example.com", FullName = "A", Professions = ["Fitness"] }]);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.HasProfessionFilter);
    }

    // A stale selection must not silently hide every provider after a reload that no longer has it.
    [Fact]
    public async Task AReloadDropsASelectionThatNoLongerExists()
    {
        var vm = CustomerFacing(TwoProfessions());
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectProfessionCommand.Execute("Tutoring");

        var narrowed = new Mock<IProviderApiService>();
        narrowed.Setup(p => p.GetProvidersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new CustomerSummary { Email = "coach@example.com", Professions = ["Fitness"] }]);
        var reloaded = new CustomersViewModel(
            CreateMockCustomerApi().Object, narrowed.Object, CreateMockSession(role: "Customer").Object)
        { SelectedProfession = "Tutoring" };

        await reloaded.LoadCommand.ExecuteAsync(null);

        Assert.Null(reloaded.SelectedProfession);
        Assert.Single(reloaded.Customers);
    }

    // A Provider viewing their customers has no profession dimension at all.
    [Fact]
    public async Task ProviderRole_HasNoProfessionFilter()
    {
        var vm = new CustomersViewModel(
            CreateMockCustomerApi([new() { Email = "c@example.com", FullName = "C" }]).Object,
            CreateMockProviderApi().Object,
            CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.AvailableProfessions);
        Assert.False(vm.HasProfessionFilter);
    }
}
