namespace AgendaBuddy.MobileApp.Models;

public sealed record AppointmentBookingResult(string? Identifier, MobileError? Error)
{
    public bool Succeeded => !string.IsNullOrWhiteSpace(Identifier);
    public string? ErrorMessage => Error?.DiagnosticDetail ?? Error?.Message;
    public bool IsSlotConflict => Error?.DiagnosticDetail?.Contains(
        "overlap", StringComparison.OrdinalIgnoreCase) == true;

    public static AppointmentBookingResult Booked(string identifier) => new(identifier, null);

    public static AppointmentBookingResult Refused(
        System.Net.HttpStatusCode status, string? diagnosticDetail = null) =>
        new(null, MobileError.FromStatus(MobileOperation.Booking, status, diagnosticDetail));

    public static AppointmentBookingResult Unreachable(string? diagnosticDetail = null) =>
        new(null, MobileError.Unavailable(MobileOperation.Booking, diagnosticDetail));
}