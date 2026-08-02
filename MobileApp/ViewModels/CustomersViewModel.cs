using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Models;
using MobileApp.Services;

namespace MobileApp.ViewModels;

public partial class CustomersViewModel : ObservableObject
{
    private readonly ICustomerApiService _customerApiService;

    [ObservableProperty]
    private List<CustomerSummary> _customers = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => !IsLoading && Customers.Count == 0 && !HasError;

    public CustomersViewModel(ICustomerApiService customerApiService)
    {
        _customerApiService = customerApiService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var results = await _customerApiService.GetCustomersAsync();

            if (results.Count == 0)
                results = SeedCustomers();

            Customers = results;
        }
        catch (HttpRequestException)
        {
            Customers = SeedCustomers();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static List<CustomerSummary> SeedCustomers() =>
    [
        new CustomerSummary { Id = "seed-c1", FullName = "Alex Chen", Email = "alex.chen@agendabuddy.dev" },
        new CustomerSummary { Id = "seed-c2", FullName = "Priya Sharma", Email = "priya.sharma@agendabuddy.dev" },
        new CustomerSummary { Id = "seed-c3", FullName = "David Thompson", Email = "david.thompson@agendabuddy.dev" }
    ];

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnCustomersChanged(List<CustomerSummary> value) => OnPropertyChanged(nameof(IsEmpty));
}
