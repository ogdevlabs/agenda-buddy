using System.Collections.ObjectModel;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>The providers this customer has hidden, and the way back for each one.</summary>
public partial class HiddenProvidersViewModel : ObservableObject
{
    private readonly IShowcaseApiService _api;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(IsEmpty))]
    private string _errorMessage = string.Empty;

    public HiddenProvidersViewModel(IShowcaseApiService api)
    {
        _api = api;
        Providers.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    public ObservableCollection<HiddenProvider> Providers { get; } = new();

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>The empty state, shown only once the list has answered — a failed load is not "nobody hidden".</summary>
    public bool IsEmpty => Providers.Count == 0 && !IsLoading && !HasError;

    [RelayCommand]
    private Task LoadAsync() => LoadCoreAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            await LoadCoreAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task LoadCoreAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _api.GetHiddenAsync();
            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = ShowcaseErrorCopy.Describe(result);
                return;
            }

            Providers.Clear();
            foreach (var provider in result.Value.OrderBy(p => p.FullName, StringComparer.CurrentCultureIgnoreCase))
                Providers.Add(provider);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UnhideAsync(HiddenProvider? provider)
    {
        if (provider is null)
            return;

        ErrorMessage = string.Empty;
        var result = await _api.UnhideAsync(provider.ProviderRef);
        if (!result.IsSuccess)
        {
            ErrorMessage = ShowcaseErrorCopy.Describe(result);
            return;
        }

        Providers.Remove(provider);
        await ToastNotifier.ShowAsync(string.IsNullOrWhiteSpace(provider.FirstName)
            ? AppResources.GetString("HiddenProviders_UnhiddenNoName")
            : AppResources.Format("HiddenProviders_Unhidden", provider.FirstName.Trim()));
    }
}
