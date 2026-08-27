namespace AgendaBuddy.Customer.Core.Commands;

// The customer's own list is the side that must succeed -- that's the record the caller is actually
// asking to change. The provider's reciprocal SubscribedCustomerCollection is cleaned up best-effort
// afterward and deliberately does NOT gate the result: a provider that no longer exists should never
// block a customer from clearing a stale reference out of their own subscriptions.
public class UnsubscribeFromProviderCommandHandler(
    ICustomerService customerService,
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<UnsubscribeFromProviderCommand, Result<CustomerEntity>>
{
    public async Task<Result<CustomerEntity>> Handle(UnsubscribeFromProviderCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var customer = await customerService.UnsubscribeFromProviderAsync(request.CustomerEmail, request.ProviderEmail);
        if (customer is null)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(UnsubscribeFromProviderCommand),
                Data = JsonSerializer.Serialize(request)
            });
            return Result.Fail<CustomerEntity>($"No customer found with email {request.CustomerEmail}");
        }

        await providerService.UnsubscribeCustomerAsync(request.ProviderEmail, request.CustomerEmail);

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(UnsubscribeFromProviderCommand),
            Data = JsonSerializer.Serialize(customer)
        });
        return Result.Ok(customer);
    }
}
