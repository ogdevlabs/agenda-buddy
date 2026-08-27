namespace AgendaBuddy.Customer.Core.Queries;

public class GetSubscribedProvidersQueryHandler(
    ICustomerService customerService,
    IEventStore eventStore)
    : IRequestHandler<GetSubscribedProvidersQuery, Result<List<string>>>
{
    public async Task<Result<List<string>>> Handle(GetSubscribedProvidersQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var customer = await customerService.FindCustomerAsync(SupportTools<CustomerEntity>.FilterByEmail(request.CustomerEmail));
        if (customer is null)
        {
            await eventStore.SaveAsync(QueryAudit.Failure(nameof(GetSubscribedProvidersQuery)));
            return Result.Fail<List<string>>($"No customer found with email {request.CustomerEmail}");
        }

        var subscriptions = customer.SubscribedProviderCollection ?? [];
        await eventStore.SaveAsync(QueryAudit.Success(nameof(GetSubscribedProvidersQuery), subscriptions.Count));
        return Result.Ok(subscriptions);
    }
}
