namespace EventAndCommands.Queries.Customers;

[ExcludeFromCodeCoverage]
public class GetCustomersQuery : IRequest<List<CustomerEntity>>
{
    public List<CustomerEntity>? CustomerEntities { get; }
}