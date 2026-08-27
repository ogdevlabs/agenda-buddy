namespace AgendaBuddy.EventAndCommands.Commands.Profession;

public class AddProfessionCommand : IRequest<ProfessionEntity>
{
    public required ProfessionEntity ProfessionEntity { get; set; }
}
