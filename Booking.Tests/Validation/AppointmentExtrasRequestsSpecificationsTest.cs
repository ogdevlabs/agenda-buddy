using Booking.Requests;
using Booking.Validation;
using Validot;
using Xunit;

namespace Booking.Tests.Validation;

// F-019-T02 spike. AppointmentStatusRequest, NoteRequest and PaymentRequest
// (Booking/Requests/AppointmentExtrasRequests.cs) have ZERO MiniValidator annotations today -- these
// specs are authored to prove the Validot pattern compiles and runs, but are NOT wired into any route
// in this task (that's T05/T06's job). See docs/pdlc/design/api-refactor-pilot-booking/
// validot-spike-findings.md for the full diff list, including which rules here are genuinely new
// behavior versus "not ported" no-ops.
public class AppointmentExtrasRequestsSpecificationsTest
{
    [Fact]
    public void AppointmentStatusRequestSpec_AnyStatusString_NoErrors_NoValidationExistsToday()
    {
        var validator = Validot.Validator.Factory.Create(AppointmentExtrasRequestsSpecifications.StatusSpec);

        var result = validator.Validate(new AppointmentStatusRequest("not-a-real-status"));

        Assert.False(result.AnyErrors);
    }

    [Fact]
    public void NoteRequestSpec_EmptyContent_AnyErrors_NewBehaviorNotPortedFromToday()
    {
        var validator = Validot.Validator.Factory.Create(AppointmentExtrasRequestsSpecifications.NoteSpec);

        var result = validator.Validate(new NoteRequest(""));

        Assert.True(result.AnyErrors);
    }

    [Fact]
    public void NoteRequestSpec_NonEmptyContent_NoErrors()
    {
        var validator = Validot.Validator.Factory.Create(AppointmentExtrasRequestsSpecifications.NoteSpec);

        var result = validator.Validate(new NoteRequest("Went well."));

        Assert.False(result.AnyErrors);
    }

    [Fact]
    public void PaymentRequestSpec_NegativeAmount_NoErrors_NoValidationExistsToday()
    {
        var validator = Validot.Validator.Factory.Create(AppointmentExtrasRequestsSpecifications.PaymentSpec);

        var result = validator.Validate(new PaymentRequest(-100m, null));

        Assert.False(result.AnyErrors);
    }

    [Fact]
    public void PaymentRequestSpec_NullCurrency_NoErrors_CurrencyIsNullable()
    {
        var validator = Validot.Validator.Factory.Create(AppointmentExtrasRequestsSpecifications.PaymentSpec);

        var result = validator.Validate(new PaymentRequest(50m, null));

        Assert.False(result.AnyErrors);
    }
}
