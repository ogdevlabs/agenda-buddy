namespace EventAndCommands.Queries.Provider;

public class GetProviderByEmailQueryHandler(IMediator mediator, ProviderService providerService, string email)
    : IRequestHandler<GetProviderByEmailQuery, ProviderEntity>
{
    public async Task<ProviderEntity> Handle(GetProviderByEmailQuery request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetProviderByEmailEvent{Email = email}, cancellationToken);
        var filter = SupportTools<ProviderEntity>.FilterByEmail(email);
        var providerEntity = await providerService.FindProviders(filter);
        return providerEntity;
    }
}