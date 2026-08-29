using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>Catalog browse for everyone, plus a provider's own selection/CRUD from that catalog — see
/// <see cref="Routing.ProfessionRouteBuilder"/>'s remarks.</summary>
public partial class ProfessionsViewModel : ObservableObject
{
    private readonly IProfessionApiService _professionApiService;
    private readonly IUserSessionService _session;

    private List<ProfessionItem> _catalog = new();

    [ObservableProperty]
    private List<ProfessionItem> _filteredCatalog = new();

    [ObservableProperty]
    private List<string> _currentProfessions = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isRemoving;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool IsProvider => _session.IsProvider;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsEmpty => !IsLoading && FilteredCatalog.Count == 0 && !HasError;
    public bool HasSelectionChanges => IsProvider && _catalog.Any(p => p.IsSelected && !CurrentProfessions.Contains(p.Name));
    public bool HasAnyProfession => IsProvider && CurrentProfessions.Count > 0;

    public ProfessionsViewModel(IProfessionApiService professionApiService, IUserSessionService session)
    {
        _professionApiService = professionApiService;
        _session = session;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        await _session.RefreshAsync();
        OnPropertyChanged(nameof(IsProvider));

        try
        {
            _catalog = await _professionApiService.GetProfessionsAsync();

            CurrentProfessions = IsProvider
                ? await _professionApiService.GetProviderProfessionsAsync(_session.Email)
                : new List<string>();

            foreach (var item in _catalog)
                item.IsSelected = CurrentProfessions.Contains(item.Name);

            ApplyFilter();
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load professions. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
            NotifySelectionChanged();
        }
    }

    [RelayCommand]
    private void ToggleSelect(ProfessionItem item)
    {
        item.IsSelected = !item.IsSelected;
        NotifySelectionChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelectionChanges))]
    private async Task SaveSelectionAsync()
    {
        var newlySelected = _catalog
            .Where(p => p.IsSelected && !CurrentProfessions.Contains(p.Name))
            .Select(p => p.Name)
            .ToList();
        if (newlySelected.Count == 0)
            return;

        IsSaving = true;
        ErrorMessage = string.Empty;

        try
        {
            var succeeded = await _professionApiService.AddProfessionsToProviderAsync(_session.Email, newlySelected);
            if (!succeeded)
            {
                ErrorMessage = "Could not save your professions — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            CurrentProfessions = CurrentProfessions.Concat(newlySelected).ToList();
            NotifySelectionChanged();
            await ToastNotifier.ShowAsync("Professions saved.");
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

    [RelayCommand]
    private async Task RemoveCurrentAsync(string professionName)
    {
        IsRemoving = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _professionApiService.RemoveProfessionFromProviderAsync(_session.Email, professionName);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Could not remove this profession — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            CurrentProfessions = CurrentProfessions.Where(p => p != professionName).ToList();
            var catalogItem = _catalog.FirstOrDefault(p => p.Name == professionName);
            if (catalogItem is not null)
                catalogItem.IsSelected = false;
            NotifySelectionChanged();
            await ToastNotifier.ShowAsync("Profession removed.");
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

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectionChanges));
        SaveSelectionCommand.NotifyCanExecuteChanged();
    }

    private void ApplyFilter()
    {
        FilteredCatalog = string.IsNullOrWhiteSpace(SearchText)
            ? _catalog
            : _catalog.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnFilteredCatalogChanged(List<ProfessionItem> value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnCurrentProfessionsChanged(List<string> value) => OnPropertyChanged(nameof(HasAnyProfession));
}
