using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

public interface IPaymentAccountApiService
{
    Task<PaymentAccountStatus?> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<PaymentOnboardingLink?> BeginCustomerSetupAsync(CancellationToken cancellationToken = default);
    Task<PaymentOnboardingLink?> BeginProviderOnboardingAsync(CancellationToken cancellationToken = default);
    Task<PaymentAccountStatus?> CompleteCustomerSetupAsync(
        string sessionId, CancellationToken cancellationToken = default);
}

public sealed class PaymentSetupUnavailableException : Exception
{
}