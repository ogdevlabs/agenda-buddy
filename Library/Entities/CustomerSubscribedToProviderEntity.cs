namespace Library.Entities;

public class CustomerSubscribedToProviderEntity
{
    public required string CustomerEmail { get; set; }
    public required string ProviderEmail { get; set; }
}