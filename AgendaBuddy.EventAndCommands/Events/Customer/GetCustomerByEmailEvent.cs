namespace AgendaBuddy.EventAndCommands.Events.Customer;

public class GetCustomerByEmailEvent : INotification
{
    public string? Email { get; set; }
}
