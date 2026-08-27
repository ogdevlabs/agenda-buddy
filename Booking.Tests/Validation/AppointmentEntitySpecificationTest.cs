using System;
using Booking.Validation;
using AgendaBuddy.Library.Entities;
using Validot;
using Xunit;

namespace Booking.Tests.Validation;

// F-019-T02 spike. Constructs the IValidator<AppointmentEntity> exactly as Program.cs registers it
// (Validator.Factory.Create(AppointmentEntitySpecification.Spec)) and asserts it enforces the same
// rule MiniValidator.TryValidate enforces today at POST /appointments: only [EmailAddress] on
// EmailProvider/EmailCustomer (Library/Entities/AppointmentEntity.cs:26-32). There is no [Required]
// on the class today, and EmailAddressAttribute.IsValid(null) returns true (confirmed against the
// live System.ComponentModel.DataAnnotations implementation, not assumed) -- only "" and a malformed
// string are rejected. That is why the spec below is .Optional() + .Email(), not .Required().
public class AppointmentEntitySpecificationTest
{
    private static readonly IValidator<AppointmentEntity> Validator =
        Validot.Validator.Factory.Create(AppointmentEntitySpecification.Spec);

    [Fact]
    public void Validate_MalformedProviderEmail_AnyErrors_MatchesMiniValidatorToday()
    {
        var appointment = MakeAppointment(provider: "not-an-email");

        var result = Validator.Validate(appointment);

        Assert.True(result.AnyErrors);
        Assert.Contains("EmailProvider", result.MessageMap.Keys);
    }

    [Fact]
    public void Validate_EmptyCustomerEmail_AnyErrors_MatchesMiniValidatorToday()
    {
        // MiniValidator/[EmailAddress] rejects "" today (EmailAddressAttribute.IsValid("") == false),
        // even though there is no [Required] on the class. The Validot spec must reject it too.
        var appointment = MakeAppointment(customer: "");

        var result = Validator.Validate(appointment);

        Assert.True(result.AnyErrors);
        Assert.Contains("EmailCustomer", result.MessageMap.Keys);
    }

    [Fact]
    public void Validate_BothEmailsWellFormed_NoErrors()
    {
        var appointment = MakeAppointment();

        var result = Validator.Validate(appointment);

        Assert.False(result.AnyErrors);
    }

    private static AppointmentEntity MakeAppointment(
        string provider = "provider@example.com",
        string customer = "customer@example.com") =>
        new()
        {
            EmailProvider = provider,
            EmailCustomer = customer,
            Start = DateTime.UtcNow.AddHours(1),
            End = DateTime.UtcNow.AddHours(2)
        };
}
