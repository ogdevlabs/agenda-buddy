namespace AgendaBuddy.Customer.Domain.Commands;

// F-020-T12: carries Email directly, rather than the handler's own former constructor parameter
// (Requests/RequestCollection.cs, deleted) -- the per-request email comes from the command, not a
// per-instance constructor argument.
[ExcludeFromCodeCoverage]
public class UpdateCustomerCommand : IRequest<Result<CustomerEntity>>
{
    public required string Email { get; set; }
    public required CustomerEntity CustomerEntity { get; set; }
}
