namespace Booking.Validation;

/// <summary>
/// F-019-T02 Validot spike. Mirrors exactly what <c>MiniValidator.TryValidate</c> enforces today for
/// <see cref="AppointmentEntity"/> at <c>POST /appointments</c>: only <c>[EmailAddress]</c> on
/// <see cref="AppointmentEntity.EmailProvider"/> and <see cref="AppointmentEntity.EmailCustomer"/>
/// (<c>Library/Entities/AppointmentEntity.cs:26-32</c>). There is no <c>[Required]</c> on the class
/// today, and <c>EmailAddressAttribute.IsValid(null)</c> returns <c>true</c> -- only <c>""</c> and a
/// malformed string are rejected. <c>.Optional()</c> reproduces that "null is fine" behavior;
/// <c>.Required()</c> would not, since Validot's <c>.Required()</c> alone treats <c>""</c> as valid
/// and only rejects <c>null</c> -- the opposite split from what this field needs.
/// <c>EmailValidationMode.DataAnnotationsCompatible</c> delegates to the same
/// <c>EmailAddressAttribute</c> logic MiniValidator already uses, so the two paths can't diverge on
/// what counts as a valid email.
/// </summary>
public static class AppointmentEntitySpecification
{
    public static readonly Specification<AppointmentEntity> Spec = s => s
        .Member(m => m.EmailProvider, m => m.Optional().Email(EmailValidationMode.DataAnnotationsCompatible))
        .And()
        .Member(m => m.EmailCustomer, m => m.Optional().Email(EmailValidationMode.DataAnnotationsCompatible));
}
