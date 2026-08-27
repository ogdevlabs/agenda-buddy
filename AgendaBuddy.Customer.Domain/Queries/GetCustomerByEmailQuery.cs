namespace AgendaBuddy.Customer.Domain.Queries;

// Carries Email directly, rather than a per-instance constructor parameter.
[ExcludeFromCodeCoverage]
public class GetCustomerByEmailQuery : IRequest<Result<CustomerEntity>>
{
    public required string Email { get; set; }
}
