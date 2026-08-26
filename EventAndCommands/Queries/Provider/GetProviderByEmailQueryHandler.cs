namespace EventAndCommands.Queries.Provider;

public class GetProviderByEmailQueryHandler(IMediator mediator, ProviderService providerService, string email, IEventStore eventStore)
    : IRequestHandler<GetProviderByEmailQuery, ProviderEntity>
{

    public async Task<ProviderEntity> Handle(GetProviderByEmailQuery request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetProviderByEmailEvent { Email = email }, cancellationToken);

        var filter = SupportTools<ProviderEntity>.FilterByEmail(email);
        var providerEntity = await providerService.FindProvidersAsync(filter);
        if (providerEntity != null)
        {
            await eventStore.SaveAsync(QueryAudit.Success("GetProviderByEmailQuery", 1));
            return providerEntity;
        }

        await eventStore.SaveAsync(QueryAudit.Failure("GetProviderByEmailQuery"));
        return null!;
    }
}
