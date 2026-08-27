using AgendaBuddy.Booking.Validation;
using Validot;

namespace AgendaBuddy.Booking.Tests.Validation;

// F-019-T02 spike, corrected at Party Review. StatusSpec/PaymentSpec (and their tests) were deleted
// as dead code -- authored, unit-tested, but never wired into a route (see
// AppointmentExtrasRequestsSpecifications.cs's remarks). NoteSpec is now wired into AgendaBuddy.Booking.Api's two
// note-content routes, replacing their inline IsNullOrWhiteSpace check.
public class AppointmentExtrasRequestsSpecificationsTest
{
    [Fact]
    public void NoteRequestSpec_EmptyContent_AnyErrors()
    {
        var validator = Validot.Validator.Factory.Create(AppointmentExtrasRequestsSpecifications.NoteSpec);

        var result = validator.Validate(new NoteRequest(""));

        Assert.True(result.AnyErrors);
    }

    [Fact]
    public void NoteRequestSpec_WhitespaceOnlyContent_AnyErrors_MatchesIsNullOrWhiteSpaceToday()
    {
        // Party Review found the original .Required().NotEmpty() spec would have let this through --
        // .NotEmpty() only rejects null/"", not "   ". Fixed to .NotWhiteSpace(), verified live
        // against the real Validot assembly to match !string.IsNullOrWhiteSpace(x) exactly.
        var validator = Validot.Validator.Factory.Create(AppointmentExtrasRequestsSpecifications.NoteSpec);

        var result = validator.Validate(new NoteRequest("   "));

        Assert.True(result.AnyErrors);
    }

    [Fact]
    public void NoteRequestSpec_NonEmptyContent_NoErrors()
    {
        var validator = Validot.Validator.Factory.Create(AppointmentExtrasRequestsSpecifications.NoteSpec);

        var result = validator.Validate(new NoteRequest("Went well."));

        Assert.False(result.AnyErrors);
    }
}
