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
            _allContacts = results;
        }
        catch (Exception)
        {
            // Real failure (network, timeout, malformed response, ambiguous write, etc.) — surface it
            // through the error banner rather than masking it with fabricated data (F-015-T08, AC8).
            ErrorMessage = _session.IsCustomer
                ? "Could not load providers. Check your connection and try again."
                : "Could not load customers. Check your connection and try again.";
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

#if MOBILE
        if (Application.Current?.Windows.FirstOrDefault()?.Page is { } page)
            await page.DisplayAlertAsync(title, body, "OK");
#else
        // The net10.0 fallback slice (MobileWorkloads=false) builds with UseMaui=false, so there is
        // no Application object and no page to present on. The text above is still built, keeping
        // this the only platform-conditional part of the view model.
        await Task.CompletedTask;
#endif
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
}
