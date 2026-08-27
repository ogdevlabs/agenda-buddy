namespace AgendaBuddy.Customer.Domain.Commands;

// Carries Email directly -- the per-request email comes from the command, not a per-instance
// constructor argument.
[ExcludeFromCodeCoverage]
public class UpdateCustomerCommand : IRequest<Result<CustomerEntity>>
{
    public required string Email { get; set; }
    public required CustomerEntity CustomerEntity { get; set; }
}
