namespace Library.Services;

public interface IPaymentService
{
    Task<PaymentEntity> ChargeAsync(PaymentEntity payment);
    Task<PaymentEntity?> GetByAppointmentAsync(string appointmentIdentifier);
    Task<PaymentEntity> RefundAsync(string appointmentIdentifier);
}
