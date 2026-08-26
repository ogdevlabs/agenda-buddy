namespace EventAndCommands.Events.Customer;

[ExcludeFromCodeCoverage]
public class UpdateCustomerEvent : INotification
{
    public CustomerEntity? CustomerEntity { get; set; }
}
