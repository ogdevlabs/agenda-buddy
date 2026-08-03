using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Models;
using MobileApp.Services;

namespace MobileApp.ViewModels;

public partial class CustomersViewModel : ObservableObject
{
    private readonly ICustomerApiService _customerApiService;
    private readonly IUserSessionService _session;
    private List<CustomerSummary> _allContacts = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private List<CustomerSummary> _customers = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _pageTitle = "Customers";

    [ObservableProperty]
    private string _searchPlaceholder = "Search customers...";

    [ObservableProperty]
    private string _emptyTitle = "No customers yet";

    [ObservableProperty]
    private string _emptySubtitle = "Once a client books a session with you, they will appear here.";

    [ObservableProperty]
    private string _searchText = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => !IsLoading && Customers.Count == 0 && !HasError;

    public CustomersViewModel(ICustomerApiService customerApiService, IUserSessionService session)
    {
        _customerApiService = customerApiService;
        _session = session;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Customers = new List<CustomerSummary>(_allContacts);
            return;
        }

        var query = SearchText.Trim();
        Customers = _allContacts
            .Where(c => c.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || c.LastSession.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        await _session.RefreshAsync();

        if (_session.IsCustomer)
        {
            PageTitle = "Providers";
            SearchPlaceholder = "Search by name or service...";
            EmptyTitle = "No providers yet";
            EmptySubtitle = "Browse and subscribe to providers to book appointments.";
        }
        else
        {
            PageTitle = "Customers";
            SearchPlaceholder = "Search customers...";
            EmptyTitle = "No customers yet";
            EmptySubtitle = "Once a client books a session with you, they will appear here.";
        }

        try
        {
            var results = await _customerApiService.GetCustomersAsync();

            if (results.Count == 0)
                results = SeedContacts();

            _allContacts = results;
        }
        catch (HttpRequestException)
        {
            _allContacts = SeedContacts();
        }
        finally
        {
            IsLoading = false;
            ApplyFilter();
        }
    }

    [RelayCommand]
    private void ToggleCustomer(CustomerSummary customer)
    {
        customer.IsExpanded = !customer.IsExpanded;
    }

    [RelayCommand]
    private async Task ShowSessionsAsync(CustomerSummary customer)
    {
        var title = customer.IsProvider
            ? $"{customer.FullName}'s Services"
            : $"Sessions with {customer.FullName}";

        var body = customer.IsProvider
            ? $"{customer.TotalSessions} services available\n\n{customer.LastSession}\n\nAvailable: {customer.Availability}"
            : $"{customer.TotalSessions} total sessions\n\nLast: {customer.LastSession}\n\nContact: {customer.Phone}";

        if (Application.Current?.Windows.FirstOrDefault()?.Page is { } page)
            await page.DisplayAlertAsync(title, body, "OK");
    }

    private List<CustomerSummary> SeedContacts()
    {
        if (_session.IsCustomer)
        {
            return
            [
                new CustomerSummary
                {
                    Id = "seed-p1", FullName = "Sarah Mitchell", Email = "sarah.mitchell@agendabuddy.dev",
                    Phone = "+1 (415) 555-0101", LastSession = "Personal Training, Yoga, HIIT, Meditation",
                    TotalSessions = 4, IsProvider = true, Availability = "Mon–Fri, 8am – 6pm"
                },
                new CustomerSummary
                {
                    Id = "seed-p2", FullName = "James Rodriguez", Email = "james.rodriguez@agendabuddy.dev",
                    Phone = "+1 (415) 555-0203", LastSession = "Piano, Music Theory, Sight Reading",
                    TotalSessions = 3, IsProvider = true, Availability = "Tue–Sat, 10am – 7pm"
                }
            ];
        }

        return
        [
            new CustomerSummary
            {
                Id = "seed-c1", FullName = "Alex Chen", Email = "alex.chen@agendabuddy.dev",
                Phone = "+1 (415) 555-0142", LastSession = "Personal Training", TotalSessions = 12
            },
            new CustomerSummary
            {
                Id = "seed-c2", FullName = "Priya Sharma", Email = "priya.sharma@agendabuddy.dev",
                Phone = "+1 (628) 555-0198", LastSession = "Yoga Session", TotalSessions = 8
            },
            new CustomerSummary
            {
                Id = "seed-c3", FullName = "David Thompson", Email = "david.thompson@agendabuddy.dev",
                Phone = "+1 (510) 555-0267", LastSession = "HIIT Coaching", TotalSessions = 3
            }
        ];
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
}
