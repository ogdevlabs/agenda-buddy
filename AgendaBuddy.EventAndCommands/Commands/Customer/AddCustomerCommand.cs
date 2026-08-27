namespace AgendaBuddy.EventAndCommands.Commands.Customer;

public class AddCustomerCommand : IRequest<string>
{
    public required CustomerEntity CustomerEntity { get; set; }
}
