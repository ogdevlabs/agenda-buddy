using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// A provider's own service catalogue — <c>GET/PUT /api/v1/services/{email}</c>. Listing, editing and
/// removing only; creating a service belongs to <see cref="AddServiceViewModel"/> and its own page.
/// </summary>
public partial class ServicesViewModel : ObservableObject
{
    private readonly IServicesApiService _servicesApiService;
    private readonly IUserSessionService _session;

    [ObservableProperty]
    private List<ServiceItem> _services = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isRemoving;

    public event EventHandler<ServiceItem>? RemoveRequested;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsEmpty => !IsLoading && Services.Count == 0 && !HasError;

    public ServicesViewModel(IServicesApiService servicesApiService, IUserSessionService session)
    {
        _servicesApiService = servicesApiService;
        _session = session;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        await _session.RefreshAsync();

        try
        {
            Services = await _servicesApiService.GetServicesAsync(_session.Email);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.GetString("Error_LoadServices");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleEdit(ServiceItem service) => service.IsEditing = !service.IsEditing;

    [RelayCommand]
    private async Task SaveServiceAsync(ServiceItem service)
    {
        ErrorMessage = string.Empty;

        try
        {
            var succeeded = await _servicesApiService.UpdateServicesAsync(_session.Email, new List<ServiceItem> { service });
            if (!succeeded)
            {
                ErrorMessage = AppResources.GetString("Error_UpdateService");
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            service.IsEditing = false;
            await ToastNotifier.ShowAsync(AppResources.GetString("Action_ServiceUpdated"));
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.Error_ServerUnavailable;
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
    }

    /// <summary>Raised so the page can confirm before actually removing — a destructive action.</summary>
    [RelayCommand]
    private void RequestRemove(ServiceItem service) => RemoveRequested?.Invoke(this, service);

    public async Task RemoveConfirmedAsync(ServiceItem service)
    {
        IsRemoving = true;
        ErrorMessage = string.Empty;

        try
        {
            var succeeded = await _servicesApiService.RemoveServiceAsync(_session.Email, service.Name);
            if (!succeeded)
            {
                ErrorMessage = AppResources.GetString("Error_RemoveService");
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            Services = Services.Where(s => s.Name != service.Name).ToList();
            await ToastNotifier.ShowAsync(AppResources.GetString("Action_ServiceRemoved"));
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.Error_ServerUnavailable;
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        finally
        {
            IsRemoving = false;
        }
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnServicesChanged(List<ServiceItem> value) => OnPropertyChanged(nameof(IsEmpty));
}
