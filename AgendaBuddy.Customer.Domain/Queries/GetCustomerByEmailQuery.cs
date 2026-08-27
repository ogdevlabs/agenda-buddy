namespace AgendaBuddy.Customer.Domain.Queries;

// F-020-T12: carries Email directly -- the pre-refactor handler (AgendaBuddy.EventAndCommands, deleted)
// took `email` as a per-instance constructor parameter instead.
[ExcludeFromCodeCoverage]
public class GetCustomerByEmailQuery : IRequest<Result<CustomerEntity>>
{
    public required string Email { get; set; }
}
