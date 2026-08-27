namespace AgendaBuddy.Services.Core.Commands;

// Typed against IProviderService, not the concrete class: it already covers everything this handler
// calls (FindProvidersAsync/UpdateProviderAsync).
public class UpdateServicesFromProviderCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore) : IRequestHandler<UpdateServicesFromProviderCommand, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(UpdateServicesFromProviderCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new UpdateServicesFromProviderEvent
        {
            Email = request.Email,
            ServiceEntities = request.ServiceEntities
        }, cancellationToken);

        var existingProvider = await providerService.FindProvidersAsync(SupportTools<ProviderEntity>.FilterByEmail(request.Email));
        if (existingProvider is null)
        {
            // Pre-existing gap (ServicesAuditTest's own remarks): no audit write on this branch, unlike
            // AddServicesToProviderCommandHandler's equivalent. Preserved, not fixed here.
            return Result.Fail<ProviderEntity>($"No provider found with email {request.Email}");
        }

        foreach (var updatedService in request.ServiceEntities)
        {
            var serviceMatch = existingProvider.ServiceEntities.SingleOrDefault(s => s.Name == updatedService.Name);
            if (serviceMatch is null) continue;
            serviceMatch.Description = updatedService.Description;
            serviceMatch.Fee = updatedService.Fee;
            serviceMatch.FeeType = updatedService.FeeType;
        }

        var updateResult = await providerService.UpdateProviderAsync(existingProvider.Id.ToString(), existingProvider);
        if (updateResult)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = nameof(UpdateServicesFromProviderCommand),
                Data = JsonSerializer.Serialize(existingProvider)
            });
            return Result.Ok(existingProvider);
        }

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = nameof(UpdateServicesFromProviderCommand),
            Data = JsonSerializer.Serialize(new ProviderEntity())
        });
        return Result.Fail<ProviderEntity>($"Failed to update provider with email {request.Email}");
    }
}
