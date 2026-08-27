namespace AgendaBuddy.EventAndCommands.Events.Customer;

[ExcludeFromCodeCoverage]
public class AddCustomerEvent : INotification
{
    public CustomerEntity? CustomerEntity { get; set; }
}
