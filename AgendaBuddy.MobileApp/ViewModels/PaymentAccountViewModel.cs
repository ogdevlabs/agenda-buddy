using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public partial class PaymentAccountViewModel(
    IPaymentAccountApiService paymentAccounts,
    IUserSessionService session,
    IInAppAlertService? alerts = null) : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isReady;

    [ObservableProperty]
    private string _status = AppResources.GetString("PaymentAccount_StatusChecking");

    [ObservableProperty]
    private string? _displayLabel;

    [ObservableProperty]
    private bool _isOnboarding;

    [ObservableProperty]
    private bool _canSkipOnboarding;

    public bool IsProvider => session.IsProvider;
    public string ActionLabel => IsProvider
        ? AppResources.PaymentAccount_ProviderAction
        : AppResources.PaymentAccount_CustomerAction;
    public bool ShowSkip => IsOnboarding && !IsReady && CanSkipOnboarding;

    public event EventHandler<Uri>? OpenUrlRequested;
    public event EventHandler? OnboardingCompleted;
    public event EventHandler? OnboardingSkipped;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var account = await paymentAccounts.GetStatusAsync();
            Apply(account);
            if (IsOnboarding && IsReady) OnboardingCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            Status = AppResources.GetString("PaymentAccount_ErrorLoad");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task BeginSetupAsync()
    {
        IsLoading = true;
        try
        {
            var link = IsProvider
                ? await paymentAccounts.BeginProviderOnboardingAsync()
                : await paymentAccounts.BeginCustomerSetupAsync();
            if (link is null)
            {
                Status = AppResources.GetString("PaymentAccount_ErrorStart");
                return;
            }

            if (link.CompletedLocally)
            {
                var account = await paymentAccounts.GetStatusAsync();
                Apply(account);
                if (alerts is not null)
                    await alerts.ShowAsync(AppResources.PaymentAccount_LocalSimulated);
                if (IsOnboarding && IsReady)
                    OnboardingCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
                OpenUrlRequested?.Invoke(this, uri);
            else
                Status = AppResources.GetString("PaymentAccount_ErrorStart");
        }
        catch (PaymentSetupUnavailableException)
        {
            Status = AppResources.PaymentAccount_StripeUnavailable;
        }
        catch (Exception)
        {
            Status = AppResources.GetString("PaymentAccount_ErrorStart");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SkipOnboarding()
    {
        if (ShowSkip)
            OnboardingSkipped?.Invoke(this, EventArgs.Empty);
    }

    private void Apply(Models.PaymentAccountStatus? account)
    {
        IsReady = account?.IsReady == true;
        CanSkipOnboarding = account?.CanSkipOnboarding == true;
        Status = account is null
            ? AppResources.GetString("PaymentAccount_StatusUnavailable")
            : AppResources.GetString(IsProvider
                ? account.IsReady ? "PaymentAccount_StatusProviderReady" : "PaymentAccount_StatusProviderRequired"
                : account.IsReady ? "PaymentAccount_StatusCustomerReady" : "PaymentAccount_StatusCustomerRequired");
        DisplayLabel = account switch
        {
            { PaymentMethodLast4.Length: > 0 } => AppResources.Format(
                "PaymentAccount_MethodEnding",
                account.PaymentMethodBrand ?? AppResources.GetString("PaymentAccount_MethodCard"),
                account.PaymentMethodLast4),
            { PaymentMethodType: "paypal" } => "PayPal",
            { IsReady: true } => AppResources.GetString("PaymentAccount_MethodSaved"),
            _ => null
        };
    }

    partial void OnIsOnboardingChanged(bool value) => OnPropertyChanged(nameof(ShowSkip));

    partial void OnIsReadyChanged(bool value) => OnPropertyChanged(nameof(ShowSkip));

    partial void OnCanSkipOnboardingChanged(bool value) => OnPropertyChanged(nameof(ShowSkip));
}