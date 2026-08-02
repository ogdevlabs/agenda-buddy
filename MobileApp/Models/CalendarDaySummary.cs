namespace MobileApp.Models;

public class CalendarDaySummary
{
    public string Date { get; set; } = string.Empty;
    public List<string> AvailableSlots { get; set; } = new();
    public List<string> BookedSlots { get; set; } = new();

    public string DayOfWeek => DateTime.TryParse(Date, out var dt) ? dt.ToString("ddd") : "";
    public string DayNumber => DateTime.TryParse(Date, out var dt) ? dt.Day.ToString() : "";
    public string MonthDay => DateTime.TryParse(Date, out var dt) ? dt.ToString("MMM d") : Date;
    public bool IsToday => DateTime.TryParse(Date, out var dt) && dt.Date == DateTime.Today;
    public bool HasBookings => BookedSlots.Count > 0;
}

public class TimeSlot
{
    public string Time { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public bool IsBooked { get; set; }
}
