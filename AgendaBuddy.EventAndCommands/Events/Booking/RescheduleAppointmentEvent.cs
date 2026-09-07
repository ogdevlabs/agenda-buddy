namespace AgendaBuddy.EventAndCommands.Events.Booking;

/// <summary>Published when a provider moves a booked session outright.</summary>
[ExcludeFromCodeCoverage]
public class RescheduleAppointmentEvent : INotification
{
    public string? Identifier { get; set; }
}

/// <summary>Published when a party proposes a new time and the appointment has not moved.</summary>
[ExcludeFromCodeCoverage]
public class RequestRescheduleEvent : INotification
{
    public string? Identifier { get; set; }
}

/// <summary>Published when an outstanding proposal is approved or declined.</summary>
[ExcludeFromCodeCoverage]
public class AnswerRescheduleEvent : INotification
{
    public string? Identifier { get; set; }
}
