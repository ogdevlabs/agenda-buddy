namespace MobileApp.Models;

public class MessageSummary
{
    public string Id { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
}
