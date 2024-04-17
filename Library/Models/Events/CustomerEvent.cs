namespace Library.Models.Events;

public class CustomerEvent
{
    public string CustomerId { get; set; }
    public string Event { get; set; }
    public DateTime TimeStamp { get; set; }
}