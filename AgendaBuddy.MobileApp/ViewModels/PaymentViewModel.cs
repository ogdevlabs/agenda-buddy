using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// Payment routes (api-contracts.md §2), wired via <see cref="IBookingApiService.GetPaymentAsync"/>.
/// The one requirement this ViewModel exists to satisfy is PRD Requirement 12 / AC13: a
/// <c>local_</c>-prefixed intent id never moved real money (ADR-038) and the copy must never claim it
/// was "Paid".
/// </summary>
public partial class PaymentViewModel : ObservableObject
{
    private const string LocalIntentPrefix = "local_";

    private readonly IBookingApiService _bookingApiService;

    [ObservableProperty]
    private PaymentEntity? _payment;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // GetAppointmentPaymentQuery answers 404 ("result.IsFailed") when no payment has been recorded YET —
    // a normal state that should offer the Pay action below, not the scary error banner LoadAsync's
    // ErrorMessage renders. IsLoading gates both so the "no payment yet" form doesn't flash before the
    // first real read completes.
    [ObservableProperty]
    private bool _hasLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PayCommand))]
    private string _payAmountInput = string.Empty;

    [ObservableProperty]
    private string _payErrorMessage = string.Empty;

    [ObservableProperty]
    private bool _isPaying;

    public string AppointmentId { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasPayment => Payment is not null;

    public bool ShowPayForm => HasLoaded && !HasPayment && !HasError;

    public bool HasPayError => !string.IsNullOrEmpty(PayErrorMessage);

    /// <summary>
    /// True when the payment's Stripe intent id is <c>local_</c>-prefixed — <c>AgendaBuddy.Library.Services.
    /// PaymentGatewayFactory</c>'s non-charging gateway (ADR-038). No money moved regardless of what
    /// <see cref="PaymentEntity.Status"/> says.
    /// </summary>
    public bool IsNonCharging =>
        Payment is not null &&
        !string.IsNullOrEmpty(Payment.StripePaymentIntentId) &&
        Payment.StripePaymentIntentId.StartsWith(LocalIntentPrefix, StringComparison.Ordinal);

    /// <summary>
    /// ux-review.md finding 1 / PRD Requirement 12 / AC13: "Payment recorded (not yet charged)" —
    /// never "Paid" — for a non-charging payment, regardless of its <see cref="PaymentStatus"/>.
    /// </summary>
    public string StatusMessage
    {
        get
        {
            if (Payment is null)
                return string.Empty;

            if (IsNonCharging)
                return "Payment recorded (not yet charged)";

            return Payment.Status switch
            {
                PaymentStatus.Succeeded => "Paid",
                PaymentStatus.Pending => "Payment pending",
                PaymentStatus.Failed => "Payment failed",
                PaymentStatus.Refunded => "Refunded",
                _ => Payment.Status.ToString()
            };
        }
    }

    public PaymentViewModel(IBookingApiService bookingApiService)
    {
        _bookingApiService = bookingApiService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        HasLoaded = false;

        try
        {
            // A 404 (no payment recorded yet) and a genuine failure are indistinguishable at this layer
            // (GetPaymentAsync returns null for either) — ShowPayForm is the honest "nothing recorded yet"
            // state, so no ErrorMessage is set here; only a thrown exception below is a real error.
            Payment = await _bookingApiService.GetPaymentAsync(AppointmentId);
        }
        catch (GatewayServiceUnavailableException ex)
        {
            ErrorMessage = GatewayErrorMapper.Describe(ex.FailedService);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not load payment details — check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
            HasLoaded = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanPay))]
    private async Task PayAsync()
    {
        if (!decimal.TryParse(PayAmountInput, out var amount) || amount <= 0)
        {
            PayErrorMessage = "Enter an amount greater than zero.";
            return;
        }

        IsPaying = true;
        PayErrorMessage = string.Empty;

        try
        {
            var created = await _bookingApiService.CreatePaymentAsync(AppointmentId, amount, currency: null);
            if (created is null)
            {
                PayErrorMessage = "Could not record this payment — try again.";
                await ToastNotifier.ShowAsync(PayErrorMessage);
                return;
            }

            Payment = created;
            await ToastNotifier.ShowAsync("Payment recorded.");
        }
        catch (GatewayServiceUnavailableException ex)
        {
            PayErrorMessage = GatewayErrorMapper.Describe(ex.FailedService);
            await ToastNotifier.ShowAsync(PayErrorMessage);
        }
        catch (HttpRequestException)
        {
            PayErrorMessage = "Could not record this payment — check your connection and try again.";
            await ToastNotifier.ShowAsync(PayErrorMessage);
        }
        finally
        {
            IsPaying = false;
        }
    }

    private bool CanPay() => !string.IsNullOrWhiteSpace(PayAmountInput);

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShowPayForm));
    }

    partial void OnHasLoadedChanged(bool value) => OnPropertyChanged(nameof(ShowPayForm));

    partial void OnPayErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasPayError));

    partial void OnPaymentChanged(PaymentEntity? value)
    {
        OnPropertyChanged(nameof(HasPayment));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(IsNonCharging));
        OnPropertyChanged(nameof(ShowPayForm));
    }
}
