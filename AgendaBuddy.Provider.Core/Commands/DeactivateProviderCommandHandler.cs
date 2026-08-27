namespace AgendaBuddy.Provider.Core.Commands;

// F-020-T11: moved from AgendaBuddy.EventAndCommands.Commands.Provider. Previously the ONLY caller
// (Provider/Program.cs's deactivate route, deleted) `new`-ed this handler directly and called
// .Handle() by hand instead of going through mediator.Send -- now a real MediatR dispatch, registered
// the same way as every other Provider command. Typed against IProviderService, which F-020-T11 had to
// extend with SetActiveAsync (see IProviderService's own remarks) to make this possible.
public class DeactivateProviderCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<DeactivateProviderCommand, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(DeactivateProviderCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(
            new DeactivateProviderEvent { ProviderEntity = request.ProviderEntity }, cancellationToken);

        var filter = SupportTools<ProviderEntity>.FilterByEmail(request.ProviderEntity.Email);
        var provider = await providerService.FindProvidersAsync(filter);

        if (provider is null)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(DeactivateProviderCommand),
                Data = JsonSerializer.Serialize(request.ProviderEntity)
            });
            return Result.Fail<ProviderEntity>($"No provider found with email {request.ProviderEntity.Email}");
        }

        // F-014 requirement 20: a targeted $set, not a whole-document replace -- see SetActiveAsync's own
        // remarks.
        await providerService.SetActiveAsync(provider.Email, isActive: false);
        provider.IsActive = false;

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(DeactivateProviderCommand),
            Data = JsonSerializer.Serialize(provider)
        });
        return Result.Ok(provider);
    }
}
