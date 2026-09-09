using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public class BookRequestedEventArgs : EventArgs
{
    public required string CounterpartEmail { get; init; }
    public required string CounterpartName { get; init; }

    /// <summary>
    /// The profession the directory was filtered to, if any — carried through so the booking screen
    /// offers only that provider's services in the same scope the customer was already browsing.
    /// </summary>
    public string? Profession { get; init; }
}

public partial class CustomersViewModel : ObservableObject
{
    private readonly ICustomerApiService _customerApiService;
    private readonly IProviderApiService _providerApiService;
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
    private string _pageTitle = AppResources.GetString("Contacts_CustomersTitle");

    [ObservableProperty]
    private string _searchPlaceholder = AppResources.GetString("Contacts_CustomersSearch");

    [ObservableProperty]
    private string _emptyTitle = AppResources.GetString("Contacts_CustomersEmptyTitle");

    [ObservableProperty]
    private string _emptySubtitle = AppResources.GetString("Contacts_CustomersEmptySubtitle");

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// The professions represented by the loaded providers — the FIRST filter layer a customer applies,
    /// before narrowing to a provider. Empty for a Provider viewing their customers.
    /// </summary>
    [ObservableProperty]
    private List<string> _availableProfessions = new();

    /// <summary>
    /// The chosen profession, or null for "all". Applied together with <see cref="SearchText"/>, so
    /// picking a profession and then typing narrows within it rather than starting over.
    /// </summary>
    [ObservableProperty]
    private string? _selectedProfession;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Only worth showing when there is something to choose between.</summary>
    public bool HasProfessionFilter => AvailableProfessions.Count > 1;

    public bool HasSelectedProfession => !string.IsNullOrWhiteSpace(SelectedProfession);

    public string SelectedProfessionLabel => AppResources.Format("Contacts_ProfessionFilter", SelectedProfession);

    public bool IsEmpty => !IsLoading && Customers.Count == 0 && !HasError;

    public event EventHandler<BookRequestedEventArgs>? BookRequested;

    public event EventHandler<CustomerSummary>? MessageRequested;

    public CustomersViewModel(ICustomerApiService customerApiService, IProviderApiService providerApiService, IUserSessionService session)
    {
        _customerApiService = customerApiService;
        _providerApiService = providerApiService;
        _session = session;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedProfessionChanged(string? value)
    {
        OnPropertyChanged(nameof(HasSelectedProfession));
        OnPropertyChanged(nameof(SelectedProfessionLabel));
        ApplyFilter();
    }

    partial void OnAvailableProfessionsChanged(List<string> value) =>
        OnPropertyChanged(nameof(HasProfessionFilter));

    [RelayCommand]
    private void ClearProfession() => SelectedProfession = null;

    [RelayCommand]
    private void SelectProfession(string? profession) =>
        // Tapping the active chip clears it, so "all" is reachable without a separate control.
        SelectedProfession = string.Equals(SelectedProfession, profession, StringComparison.OrdinalIgnoreCase)
            ? null
            : profession;

    /// <summary>
    /// Profession first, then free text within it. Both layers are applied together rather than as
    /// alternatives, because the profession is a scope and the text is a search inside that scope.
    /// </summary>
    private void ApplyFilter()
    {
        IEnumerable<CustomerSummary> filtered = _allContacts;

        if (!string.IsNullOrWhiteSpace(SelectedProfession))
            filtered = filtered.Where(c =>
                c.Professions.Contains(SelectedProfession, StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim();
            filtered = filtered.Where(c =>
                c.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || c.Email.Contains(query, StringComparison.OrdinalIgnoreCase)
                || c.LastSession.Contains(query, StringComparison.OrdinalIgnoreCase)
                || c.Professions.Any(p => p.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        Customers = filtered.ToList();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        await _session.RefreshAsync();

        if (_session.IsCustomer)
        {
            PageTitle = AppResources.GetString("Contacts_ProvidersTitle");
            SearchPlaceholder = AppResources.GetString("Contacts_ProvidersSearch");
            EmptyTitle = AppResources.GetString("Contacts_ProvidersEmptyTitle");
            EmptySubtitle = AppResources.GetString("Contacts_ProvidersEmptySubtitle");
        }
        else
        {
            PageTitle = AppResources.GetString("Contacts_CustomersTitle");
            SearchPlaceholder = AppResources.GetString("Contacts_CustomersSearch");
            EmptyTitle = AppResources.GetString("Contacts_CustomersEmptyTitle");
            EmptySubtitle = AppResources.GetString("Contacts_CustomersEmptySubtitle");
        }

        try
        {
            if (_session.IsCustomer)
            {
                // Bug fix: this used to call GetCustomersAsync (GET /api/v1/customers) unconditionally, which
                // is Provider-role-gated server-side — a Customer got a 403 every time they opened this tab.
                // The real capability for a Customer here is the provider directory.
                var providers = await _providerApiService.GetProvidersAsync();
                var subscriptions = await _customerApiService.GetSubscriptionsAsync(_session.Email);
                foreach (var provider in providers)
                {
                    provider.IsSubscribed = subscriptions.Contains(provider.Email, StringComparer.OrdinalIgnoreCase);

                    // Subscribing is what opens the channel, so the button appears on the same condition
                    // the server enforces — no Message button on a provider you have only browsed past.
                    provider.CanMessage = provider.IsSubscribed;
                }

                _allContacts = providers;

                // Only professions that actually have a bookable provider behind them — offering a chip
                // that filters to nothing is worse than not offering it.
                AvailableProfessions = providers
                    .SelectMany(provider => provider.Professions)
                    .Where(profession => !string.IsNullOrWhiteSpace(profession))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(profession => profession, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (SelectedProfession is not null
                    && !AvailableProfessions.Contains(SelectedProfession, StringComparer.OrdinalIgnoreCase))
                {
                    SelectedProfession = null;
                }
            }
            else
            {
                var customers = await _customerApiService.GetCustomersAsync();

                // GET /api/v1/customers is a Provider-role-gated paged read of the WHOLE customer table
                // (ADR-026) — not this provider's own subscribers. Messaging is only permitted along a
                // subscription, so the button is gated per row rather than granted to every row: offering it on
                // a stranger produced a 403 the client then reported as a connection failure.
                //
                // Read off the row itself, so the gate is exactly as fresh as the card it sits on.
                foreach (var customer in customers)
                    customer.CanMessage =
                        customer.SubscribedProviders.Contains(_session.Email, StringComparer.OrdinalIgnoreCase);

                _allContacts = customers;
                AvailableProfessions = [];
                SelectedProfession = null;
            }
        }
        catch (Exception)
        {
            // Real failure (network, timeout, malformed response, ambiguous write, etc.) — surface it
            // through the error banner rather than masking it with fabricated data.
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
    private async Task ToggleSubscriptionAsync(CustomerSummary provider)
    {
        if (!provider.IsProvider || provider.IsBusy)
            return;

        provider.IsBusy = true;
        try
        {
            var succeeded = provider.IsSubscribed
                ? await _customerApiService.UnsubscribeAsync(_session.Email, provider.Email)
                : await _customerApiService.SubscribeAsync(_session.Email, provider.Email);

            if (succeeded)
            {
                provider.IsSubscribed = !provider.IsSubscribed;
                provider.CanMessage = provider.IsSubscribed;
                await Infrastructure.ToastNotifier.ShowAsync(provider.IsSubscribed ? "Subscribed." : "Unsubscribed.");
            }
            else
            {
                ErrorMessage = provider.IsSubscribed
                    ? "Could not unsubscribe. Try again."
                    : "Could not subscribe. Try again.";
                await Infrastructure.ToastNotifier.ShowAsync(ErrorMessage);
            }
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.Error_ServerUnavailable;
            await Infrastructure.ToastNotifier.ShowAsync(ErrorMessage);
        }
        finally
        {
            provider.IsBusy = false;
        }
    }

    /// <summary>Opens the thread with this contact, whether or not anything has been said yet.</summary>
    [RelayCommand]
    private void Message(CustomerSummary contact) =>
        MessageRequested?.Invoke(this, contact);

    [RelayCommand]
    private void Book(CustomerSummary contact) =>
        BookRequested?.Invoke(this, new BookRequestedEventArgs
        {
            CounterpartEmail = contact.Email,
            CounterpartName = contact.FullName,
            Profession = SelectedProfession
        });

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
