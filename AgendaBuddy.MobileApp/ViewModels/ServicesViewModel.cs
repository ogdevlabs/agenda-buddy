using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>A provider's own service catalogue — <c>GET/PUT/PATCH /api/v1/services/{email}</c>.</summary>
public partial class ServicesViewModel : ObservableObject
{
    private readonly IServicesApiService _servicesApiService;
    private readonly IProfessionApiService _professionApiService;
    private readonly IUserSessionService _session;

    [ObservableProperty]
    private List<ServiceItem> _services = new();

    [ObservableProperty]
    private List<string> _availableProfessions = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddServiceCommand))]
    private string _newServiceName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddServiceCommand))]
    private string _newServiceDescription = string.Empty;

    [ObservableProperty]
    private string _newServiceFee = string.Empty;

    [ObservableProperty]
    private string _newServiceDuration = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddServiceCommand))]
    private string? _newServiceProfessionName;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isRemoving;

    public event EventHandler<ServiceItem>? RemoveRequested;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsEmpty => !IsLoading && Services.Count == 0 && !HasError;

    /// <summary>A provider with no Professions yet cannot offer any Service (AddServicesToProviderCommandHandler
    /// enforces this server-side) — the Add form is replaced with a prompt to pick a profession first.</summary>
    public bool HasNoProfessions => !IsLoading && AvailableProfessions.Count == 0;

    public ServicesViewModel(IServicesApiService servicesApiService, IProfessionApiService professionApiService, IUserSessionService session)
    {
        _servicesApiService = servicesApiService;
        _professionApiService = professionApiService;
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
            AvailableProfessions = await _professionApiService.GetProviderProfessionsAsync(_session.Email);
            NewServiceProfessionName = AvailableProfessions.FirstOrDefault();
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load your services. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddService))]
    private async Task AddServiceAsync()
    {
        decimal? fee = decimal.TryParse(NewServiceFee, out var parsedFee) ? parsedFee : null;
        int? duration = int.TryParse(NewServiceDuration, out var parsedDuration) ? parsedDuration : null;
        var newItem = new ServiceItem
        {
            Name = NewServiceName,
            Description = NewServiceDescription,
            Fee = fee,
            FeeType = FeeType.Fixed,
            DurationMinutes = duration,
            ProfessionName = NewServiceProfessionName
        };

        IsSaving = true;
        ErrorMessage = string.Empty;

        try
        {
            var succeeded = await _servicesApiService.AddServicesAsync(_session.Email, new List<ServiceItem> { newItem });
            if (!succeeded)
            {
                ErrorMessage = "Could not add this service — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            NewServiceName = string.Empty;
            NewServiceDescription = string.Empty;
            NewServiceFee = string.Empty;
            NewServiceDuration = string.Empty;
            await LoadAsync();
            await ToastNotifier.ShowAsync("Service added.");
        }
        catch (Exception)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool CanAddService() =>
        !string.IsNullOrWhiteSpace(NewServiceName)
        && !string.IsNullOrWhiteSpace(NewServiceDescription)
        && !string.IsNullOrWhiteSpace(NewServiceProfessionName);

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
                ErrorMessage = "Could not update this service — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            service.IsEditing = false;
            await ToastNotifier.ShowAsync("Service updated.");
        }
        catch (Exception)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
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
                ErrorMessage = "Could not remove this service — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            Services = Services.Where(s => s.Name != service.Name).ToList();
            await ToastNotifier.ShowAsync("Service removed.");
        }
        catch (Exception)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
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

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoProfessions));
    }

    partial void OnServicesChanged(List<ServiceItem> value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnAvailableProfessionsChanged(List<string> value) => OnPropertyChanged(nameof(HasNoProfessions));
}
