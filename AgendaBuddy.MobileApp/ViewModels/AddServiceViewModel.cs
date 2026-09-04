using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// Creating one service — <c>PATCH /api/v1/services/{email}</c>.
/// </summary>
/// <remarks>
/// Split out of <see cref="ServicesViewModel"/> so the form has a page to itself. On one page with the
/// service list, the form was the last thing in a scrolling column and its submit button sat below the
/// fold as soon as the provider had any services at all.
/// </remarks>
public partial class AddServiceViewModel : ObservableObject
{
    private readonly IServicesApiService _servicesApiService;
    private readonly IProfessionApiService _professionApiService;
    private readonly IUserSessionService _session;

    [ObservableProperty]
    private List<string> _availableProfessions = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddServiceCommand))]
    private string _serviceName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddServiceCommand))]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _fee = string.Empty;

    [ObservableProperty]
    private string _durationMinutes = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddServiceCommand))]
    private string? _professionName;

    /// <summary>Raised once the service is stored, so the page can return to the list.</summary>
    public event EventHandler? Added;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// A provider with no professions cannot offer a service — <c>AddServicesToProviderCommandHandler</c>
    /// enforces that server-side — so the form is replaced with a prompt to pick one first.
    /// </summary>
    public bool HasNoProfessions => !IsLoading && AvailableProfessions.Count == 0;

    public AddServiceViewModel(
        IServicesApiService servicesApiService,
        IProfessionApiService professionApiService,
        IUserSessionService session)
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
            AvailableProfessions = await _professionApiService.GetProviderProfessionsAsync(_session.Email);
            ProfessionName = AvailableProfessions.FirstOrDefault();
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load your professions. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddService))]
    private async Task AddServiceAsync()
    {
        decimal? fee = decimal.TryParse(Fee, out var parsedFee) ? parsedFee : null;
        int? duration = int.TryParse(DurationMinutes, out var parsedDuration) ? parsedDuration : null;
        var newItem = new ServiceItem
        {
            Name = ServiceName,
            Description = Description,
            Fee = fee,
            FeeType = FeeType.Fixed,
            DurationMinutes = duration,
            ProfessionName = ProfessionName
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

            Reset();
            await ToastNotifier.ShowAsync("Service added.");
            Added?.Invoke(this, EventArgs.Empty);
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

    private void Reset()
    {
        ServiceName = string.Empty;
        Description = string.Empty;
        Fee = string.Empty;
        DurationMinutes = string.Empty;
    }

    private bool CanAddService() =>
        !string.IsNullOrWhiteSpace(ServiceName)
        && !string.IsNullOrWhiteSpace(Description)
        && !string.IsNullOrWhiteSpace(ProfessionName);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(HasNoProfessions));

    partial void OnAvailableProfessionsChanged(List<string> value) => OnPropertyChanged(nameof(HasNoProfessions));
}
