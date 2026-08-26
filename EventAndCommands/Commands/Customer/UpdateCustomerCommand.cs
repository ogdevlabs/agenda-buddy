namespace EventAndCommands.Commands.Customer;

[ExcludeFromCodeCoverage]
public class UpdateCustomerCommand : IRequest<string>
{
    public required CustomerEntity CustomerEntity { get; set; }
}
