namespace AgendaBuddy.Services.Core.Commands;

// Typed against IProviderService, matching AddServicesToProviderCommandHandler/
// UpdateServicesFromProviderCommandHandler's own convention.
public class RemoveServiceFromProviderCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore) : IRequestHandler<RemoveServiceFromProviderCommand, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(RemoveServiceFromProviderCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new RemoveServiceFromProviderEvent
        {
            Email = request.Email,
            ServiceName = request.ServiceName
        }, cancellationToken);

        var existingProvider = await providerService.FindProvidersAsync(SupportTools<ProviderEntity>.FilterByEmail(request.Email));
        if (existingProvider is null)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(RemoveServiceFromProviderCommand),
                Data = JsonSerializer.Serialize(new ProviderEntity())
            });
            return Result.Fail<ProviderEntity>($"No provider found with email {request.Email}");
        }

        var removedCount = existingProvider.ServiceEntities.RemoveAll(s => s.Name == request.ServiceName);
        if (removedCount == 0)
        {
            // A no-op removal is still reported as a failure, not silently accepted as success — the
            // caller asked to remove a specific named service, and it wasn't there to remove.
            return Result.Fail<ProviderEntity>($"No service named '{request.ServiceName}' found for provider {request.Email}");
        }

        var updateResult = await providerService.UpdateProviderAsync(existingProvider.Id.ToString(), existingProvider);
        if (updateResult)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = nameof(RemoveServiceFromProviderCommand),
                Data = JsonSerializer.Serialize(existingProvider)
            });
            return Result.Ok(existingProvider);
        }

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = nameof(RemoveServiceFromProviderCommand),
            Data = JsonSerializer.Serialize(new ProviderEntity())
        });
        return Result.Fail<ProviderEntity>($"Failed to update provider with email {request.Email}");
    }
}
