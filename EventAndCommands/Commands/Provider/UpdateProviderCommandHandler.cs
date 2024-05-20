using EventAndCommands.Events.Provider;
using Library.Entities;
using Library.Services;
using Library.Tools;
using MediatR;
using MongoDB.Bson;

namespace EventAndCommands.Commands.Provider;

public class UpdateProviderCommandHandler : IRequestHandler<UpdateProviderCommand, string>
{
    private readonly IMediator _mediator;
    private readonly ProviderService _providerService;
    private readonly ProviderEntity _providerEntity;
    private readonly string _email;

    public UpdateProviderCommandHandler(
        string email,
        IMediator mediator, 
        ProviderService providerService,
        ProviderEntity providerEntity)
    {
        _email = email;
        _mediator = mediator;
        _providerService = providerService;
        _providerEntity = providerEntity;
    }

    public async Task<string> Handle(UpdateProviderCommand request, CancellationToken cancellationToken)
    {
        await _mediator.Publish( new UpdateProviderEvent
        {
            ProviderEntity = request.ProviderEntity
        }, cancellationToken);
        var record = await _providerService
            .FindProviders(SupportTools<ProviderEntity>.FilterByEmail(_email));
        if (record != null)
        {
            _providerEntity.Id = record.Id;
            if (await _providerService.UpdateProvider(record.Id.ToString(), _providerEntity))
            {
                return await Task.FromResult(_providerEntity.ToJson());
            }
        }
        return await Task.FromResult(string.Empty);
    }
}