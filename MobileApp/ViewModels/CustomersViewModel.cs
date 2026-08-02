using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Models;
using MobileApp.Services;

namespace MobileApp.ViewModels;

public partial class CustomersViewModel : ObservableObject
{
    private readonly ICustomerApiService _customerApiService;
    private readonly IUserSessionService _session;

    [ObservableProperty]
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

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => !IsLoading && Customers.Count == 0 && !HasError;

    public CustomersViewModel(ICustomerApiService customerApiService, IUserSessionService session)
    {
        _customerApiService = customerApiService;
        _session = session;
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
            SearchPlaceholder = "Search providers...";
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

            Customers = results;
        }
        catch (HttpRequestException)
        {
            Customers = SeedContacts();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleCustomer(CustomerSummary customer)
    {
        customer.IsExpanded = !customer.IsExpanded;
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

    partial void OnCustomersChanged(List<CustomerSummary> value) => OnPropertyChanged(nameof(IsEmpty));
}
