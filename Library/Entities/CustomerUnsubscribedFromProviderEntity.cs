namespace Library.Entities;

public class CustomerUnsubscribedFromProviderEntity
{
    public required string CustomerEmail { get; set; }
    public required string ProviderEmail { get; set; }
}