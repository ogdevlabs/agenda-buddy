namespace AgendaBuddy.Services.Core.Commands;

// F-020-T10. Typed against IProviderService, not the concrete class: it already covers everything
// this handler calls (FindProvidersAsync/UpdateProviderAsync).
public class AddServicesToProviderCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore) : IRequestHandler<AddServicesToProviderCommand, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(AddServicesToProviderCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new AddServicesToProviderEvent
        {
            Email = request.Email,
            ServiceEntities = request.ServiceEntities
        }, cancellationToken);

        var provider = await providerService.FindProvidersAsync(SupportTools<ProviderEntity>.FilterByEmail(request.Email));
        if (provider is null)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(AddServicesToProviderCommand),
                Data = JsonSerializer.Serialize(new ProviderEntity())
            });
            return Result.Fail<ProviderEntity>($"No provider found with email {request.Email}");
        }

        provider.ServiceEntities.AddRange(SupportTools<ServiceEntity>.GenerateIdForRecord(request.ServiceEntities));
        var updateResult = await providerService.UpdateProviderAsync(provider.Id.ToString(), provider);
        if (!updateResult)
        {
            // Pre-existing gap, out of this task's scope (the same one ServicesAuditTest documents for
            // UpdateServicesFromProviderCommandHandler's own not-found branch): no audit write here.
            return Result.Fail<ProviderEntity>($"Failed to update provider with email {request.Email}");
        }

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(AddServicesToProviderCommand),
            Data = JsonSerializer.Serialize(provider)
        });
        return Result.Ok(provider);
    }
}
