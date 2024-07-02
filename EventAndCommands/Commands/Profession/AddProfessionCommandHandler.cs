using EventAndCommands.Events.Profession;

namespace EventAndCommands.Commands.Profession;

[RegisterService(ServiceLifetime.Scoped)]
public class AddProfessionCommandHandler(
    IMediator mediator,
    ProfessionService professionService,
    ProfessionEntity professionEntity) : IRequestHandler<AddProfessionCommand, ProfessionEntity>
{
    [InjectService] private IEventStore EventStore { get; } = new EventStore();

    public async Task<ProfessionEntity> Handle(AddProfessionCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new AddProfessionEvent { ProfessionEntity = professionEntity }, cancellationToken);
        try
        {
            await professionService.CreateProfessionAsync(professionEntity);
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "AddProfessionCommand",
                Data = JsonSerializer.Serialize(professionEntity)
            };
            await EventStore!.SaveAsync(successEvent);
            return await Task.FromResult(professionEntity);
        }
        catch (Exception)
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "AddProfessionCommand",
                Data = JsonSerializer.Serialize(professionEntity)
            };
            await EventStore!.SaveAsync(successEvent);
            return await Task.FromResult(professionEntity);
        }
    }
}