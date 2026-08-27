using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.Library.Entities;
using MobileApp.Infrastructure;
using MobileApp.Services;

namespace MobileApp.ViewModels;

/// <summary>
/// F-014's payment routes (api-contracts.md §2), never rendered anywhere in the client before
/// F-015-T11 — F-015-T07 only wired the API calls (<see cref="IBookingApiService.GetPaymentAsync"/>).
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

    public string AppointmentId { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasPayment => Payment is not null;

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

        try
        {
            var result = await _bookingApiService.GetPaymentAsync(AppointmentId);
            if (result is null)
                ErrorMessage = "Could not load payment details — try again.";
            else
                Payment = result;
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
        }
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnPaymentChanged(PaymentEntity? value)
    {
        OnPropertyChanged(nameof(HasPayment));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(IsNonCharging));
    }
}
