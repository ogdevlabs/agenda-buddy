namespace AgendaBuddy.Customer.Domain.Queries;

[ExcludeFromCodeCoverage]
public class GetCustomersQuery : IRequest<Result<PagedResponse<CustomerEntity>>>
{
    public required PageRequest Page { get; set; }
}
