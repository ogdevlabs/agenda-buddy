using System.Net;
using AgendaBuddy.MobileApp.Resources.Strings;

namespace AgendaBuddy.MobileApp.Models;

public enum MobileErrorCategory
{
    Validation,
    Forbidden,
    Conflict,
    NotFound,
    Unavailable,
    Unknown
}

public enum MobileOperation
{
    AppointmentAction,
    Booking,
    CalendarBlock,
    ProfessionRemoval,
    WorkWeek
}

public sealed record MobileError(
    MobileOperation Operation,
    MobileErrorCategory Category,
    string? DiagnosticDetail = null)
{
    public string Message => (Operation, Category) switch
    {
        (_, MobileErrorCategory.Unavailable) => AppResources.Error_ServerUnavailable,
        (_, MobileErrorCategory.Forbidden) => AppResources.Error_Forbidden,
        (MobileOperation.Booking, MobileErrorCategory.Conflict) => AppResources.Error_BookingConflict,
        (MobileOperation.CalendarBlock, MobileErrorCategory.Conflict) => AppResources.Error_CalendarBlockConflict,
        (MobileOperation.ProfessionRemoval, MobileErrorCategory.Conflict) => AppResources.Error_ProfessionRemovalConflict,
        (MobileOperation.AppointmentAction, MobileErrorCategory.NotFound) => AppResources.Error_AppointmentNotFound,
        (MobileOperation.CalendarBlock, MobileErrorCategory.NotFound) => AppResources.Error_CalendarBlockNotFound,
        (MobileOperation.ProfessionRemoval, MobileErrorCategory.NotFound) => AppResources.Error_ProfessionNotFound,
        (_, MobileErrorCategory.Validation) => AppResources.Error_Validation,
        _ => AppResources.Error_ActionRejected
    };

    public static MobileError FromStatus(
        MobileOperation operation, HttpStatusCode status, string? diagnosticDetail = null) =>
        new(operation, status switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => MobileErrorCategory.Validation,
            HttpStatusCode.Forbidden => MobileErrorCategory.Forbidden,
            HttpStatusCode.Conflict => MobileErrorCategory.Conflict,
            HttpStatusCode.NotFound => MobileErrorCategory.NotFound,
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout =>
                MobileErrorCategory.Unavailable,
            _ => MobileErrorCategory.Unknown
        }, Normalize(diagnosticDetail));

    public static MobileError Unavailable(MobileOperation operation, string? diagnosticDetail = null) =>
        new(operation, MobileErrorCategory.Unavailable, Normalize(diagnosticDetail));

    private static string? Normalize(string? detail) =>
        string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
}