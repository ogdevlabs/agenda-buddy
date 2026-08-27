namespace AgendaBuddy.Customer.Core.Commands;

// Subscribing writes BOTH sides of the relationship: CustomerEntity.SubscribedProviderCollection and
// ProviderEntity.SubscribedCustomerCollection (the latter pre-existed, unwired -- ADR-053).
// ProviderService.SubscribeCustomerAsync's return also serves as the provider-existence check, so
// subscribing to a nonexistent provider email fails cleanly rather than growing the customer's list
// with a value nothing else can resolve.
public class SubscribeToProviderCommandHandler(
    ICustomerService customerService,
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<SubscribeToProviderCommand, Result<CustomerEntity>>
{
    public async Task<Result<CustomerEntity>> Handle(SubscribeToProviderCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var provider = await providerService.SubscribeCustomerAsync(request.ProviderEmail, request.CustomerEmail);
        if (provider is null)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(SubscribeToProviderCommand),
                Data = JsonSerializer.Serialize(request)
            });
            return Result.Fail<CustomerEntity>($"No provider found with email {request.ProviderEmail}");
        }

        var customer = await customerService.SubscribeToProviderAsync(request.CustomerEmail, request.ProviderEmail);
        if (customer is null)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(SubscribeToProviderCommand),
                Data = JsonSerializer.Serialize(request)
            });
            return Result.Fail<CustomerEntity>($"No customer found with email {request.CustomerEmail}");
        }

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(SubscribeToProviderCommand),
            Data = JsonSerializer.Serialize(customer)
        });
        return Result.Ok(customer);
    }
}
