using EventAndCommands.Events.Profession;

namespace EventAndCommands.Commands.Profession;

public class AddProfessionCommandHandler(
    IMediator mediator,
    ProfessionService professionService,
    ProfessionEntity professionEntity,
    IEventStore eventStore) : IRequestHandler<AddProfessionCommand, ProfessionEntity>
{

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
            await eventStore.SaveAsync(successEvent);
            return professionEntity;
        }
        catch (Exception)
        {
            var failEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "AddProfessionCommand",
                Data = JsonSerializer.Serialize(professionEntity)
            };
            await eventStore.SaveAsync(failEvent);
            return professionEntity;
        }
    }
}