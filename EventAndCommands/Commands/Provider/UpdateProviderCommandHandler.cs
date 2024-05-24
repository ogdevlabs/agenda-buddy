namespace EventAndCommands.Commands.Provider;

public class UpdateProviderCommandHandler(
    string email,
    IMediator mediator,
    ProviderService providerService,
    ProviderEntity providerEntity)
    : IRequestHandler<UpdateProviderCommand, string>
{
    public async Task<string> Handle(UpdateProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish( new UpdateProviderEvent
        {
            ProviderEntity = request.ProviderEntity
        }, cancellationToken);
        var record = await providerService
            .FindProviders(SupportTools<ProviderEntity>.FilterByEmail(email));
        if (record != null)
        {
            providerEntity.Id = record.Id;
            if (await providerService.UpdateProvider(record.Id.ToString(), providerEntity))
            {
                return await Task.FromResult(providerEntity.ToJson());
            }
        }
        return await Task.FromResult(string.Empty);
    }
}