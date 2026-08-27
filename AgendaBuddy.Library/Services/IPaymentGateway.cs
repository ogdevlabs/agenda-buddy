namespace AgendaBuddy.Library.Services;

public interface IPaymentGateway
{
    Task<string> CreatePaymentIntentAsync(decimal amount, string currency, string description);
    Task<bool> ConfirmPaymentIntentAsync(string paymentIntentId);
    Task<bool> RefundPaymentIntentAsync(string paymentIntentId);
}
