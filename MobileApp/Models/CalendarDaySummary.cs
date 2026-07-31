namespace MobileApp.Models;

public class CalendarDaySummary
{
    public string Date { get; set; } = string.Empty;          // ISO 8601 date
    public List<string> AvailableSlots { get; set; } = new();
    public List<string> BookedSlots { get; set; } = new();
}
