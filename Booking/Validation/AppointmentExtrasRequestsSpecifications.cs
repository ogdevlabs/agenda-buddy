namespace Booking.Validation;

/// <summary>
/// F-019-T02 Validot spike. Specs for the three F-014 request records
/// (<c>Booking/Requests/AppointmentExtrasRequests.cs</c>), which have ZERO MiniValidator
/// annotations today. <b>Not wired into any route by this task</b> -- they exist only to prove the
/// authoring pattern compiles and runs; wiring is T05/T06's job.
/// </summary>
/// <remarks>
/// See <c>docs/pdlc/design/api-refactor-pilot-booking/validot-spike-findings.md</c> for the full
/// diff list. In short:
/// <list type="bullet">
/// <item><see cref="StatusSpec"/> deliberately enforces nothing on <c>Status</c> -- there is no
/// enum-membership check today (it's validated downstream, not by MiniValidator), and adding one
/// here would be new behavior, not a port.</item>
/// <item><see cref="NoteSpec"/> rejects empty <c>Content</c>. This IS new behavior -- MiniValidator
/// enforces nothing on <c>NoteRequest</c> today -- included to demonstrate a real rule chain rather
/// than an empty spec.</item>
/// <item><see cref="PaymentSpec"/> deliberately enforces nothing on <c>Amount</c> (no positivity
/// check exists today) or <c>Currency</c> (nullable; no <c>.Required()</c> per the roundtable's
/// explicit instruction).</item>
/// </list>
/// </remarks>
public static class AppointmentExtrasRequestsSpecifications
{
    public static readonly Specification<AppointmentStatusRequest> StatusSpec = s => s;

    public static readonly Specification<NoteRequest> NoteSpec = s => s
        .Member(m => m.Content, m => m.Required().NotEmpty());

    public static readonly Specification<PaymentRequest> PaymentSpec = s => s;
}
