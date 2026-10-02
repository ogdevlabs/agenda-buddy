namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>What the calendar sync screen needs from the device, kept behind an interface so the flow is testable.</summary>
public interface ICalendarSyncDevice
{
    /// <summary>Apple Calendar is the native calendar here; elsewhere Google Calendar is.</summary>
    bool PrefersAppleCalendar { get; }

    /// <summary>Hands a link to the OS. <c>false</c> when nothing on the device would open it.</summary>
    Task<bool> OpenAsync(string url);

    Task CopyAsync(string text);

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}
