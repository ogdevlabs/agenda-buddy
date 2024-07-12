namespace EventAndCommands.Events.Profession;

public class AddProfessionsToProviderEvent : INotification
{
    public List<ProfessionEntity>? ProfessionEntities { get; set; }
}