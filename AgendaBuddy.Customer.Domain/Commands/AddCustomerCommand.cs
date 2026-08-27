namespace AgendaBuddy.Customer.Domain.Commands;

[ExcludeFromCodeCoverage]
public class AddCustomerCommand : IRequest<Result<CustomerEntity>>
{
    public required CustomerEntity CustomerEntity { get; set; }
}
