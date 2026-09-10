namespace AgendaBuddy.Library.Services;

public interface IPaymentService
{
    Task<PaymentEntity> AuthorizeAsync(
        AppointmentEntity appointment, CustomerEntity customer, ProviderEntity provider);
    Task<bool> CaptureAsync(string appointmentIdentifier);
    Task ReconcileAsync(string paymentIntentId, string externalStatus, DateTime eventCreatedAt);
    Task<PaymentEntity?> GetByAppointmentAsync(string appointmentIdentifier);
    Task<bool> ReleaseOrRefundAsync(string appointmentIdentifier);
    Task<PaymentEntity> RefundAsync(string appointmentIdentifier);
}
