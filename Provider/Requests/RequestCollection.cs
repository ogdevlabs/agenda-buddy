namespace Provider.Requests;

public class RequestCollection(IKafkaClient kafkaClient, IEventStore eventStore) : IRequestCollection
{
    public async Task<string> AddProviderRequest(
        IMediator mediator,
        ProviderService providerService,
        ProviderEntity providerEntity)
    {
        var result = await new AddProviderCommandHandler(
                mediator,
                (kafkaClient as KafkaClient)!,
                providerService,
                providerEntity,
                eventStore)
            .Handle(
                new AddProviderCommand { TopicName = providerEntity.KafkaTopic! },
                new CancellationToken());
        return result;
    }

    public async Task<string> UpdateProviderRequest(
        string email,
        IMediator mediator,
        ProviderService providerService,
        ProviderEntity providerEntity)
    {
        var result = await new UpdateProviderCommandHandler(
                email,
                mediator,
                providerService,
                providerEntity,
                eventStore)
            .Handle(
                new UpdateProviderCommand { ProviderEntity = providerEntity },
                new CancellationToken());
        return result;
    }

    public async Task<PagedResponse<ProviderEntity>> GetProvidersRequest(IMediator mediator,
        ProviderService providerService, PageRequest page)
    {
        var result =
            await new GetProvidersQueryHandler(mediator, providerService, eventStore, page).Handle(
                new GetProvidersQuery(), new CancellationToken());
        return result;
    }

    public async Task<ProviderEntity> GetProviderByEmail(IMediator mediator, ProviderService providerService,
        string email)
    {
        var result =
            await new GetProviderByEmailQueryHandler(mediator, providerService, email, eventStore).Handle(
                new GetProviderByEmailQuery(), new CancellationToken());
        return result;
    }
}
