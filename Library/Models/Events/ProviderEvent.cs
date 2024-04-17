using System.ComponentModel.DataAnnotations;

namespace Library.Models.Events;

public class ProviderEvent
{
    public string ProviderId { get; set; }
    public string Event { get; set; }
    public DateTime TimeStamp { get; set; }
}