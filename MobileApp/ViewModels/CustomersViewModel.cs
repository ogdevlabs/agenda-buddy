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
            Customers = await _customerApiService.GetCustomersAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not load customers — check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnCustomersChanged(List<CustomerSummary> value) => OnPropertyChanged(nameof(IsEmpty));
}
