namespace MobileApp.Models;

public class MessageThreadStub
{
    public string ThreadId { get; set; } = string.Empty;
    public string OtherPartyEmail { get; set; } = string.Empty;
    public string LastMessageBody { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}
