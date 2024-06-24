using System.ComponentModel;

namespace Library.Entities;

public enum AppointmentStatus
{
    [Description("Appointment Requested")]
    Requested,
    [Description("Appointment Booked")]
    Booked,
    [Description("Appointment Cancelled")]
    Cancelled,
    [Description("Appointment Completed")]
    Completed
}