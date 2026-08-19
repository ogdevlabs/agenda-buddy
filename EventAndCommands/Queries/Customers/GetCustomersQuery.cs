namespace EventAndCommands.Queries.Customers;

[ExcludeFromCodeCoverage]
public class GetCustomersQuery : IRequest<PagedResponse<CustomerEntity>>
{
    public List<CustomerEntity>? CustomerEntities { get; }
}