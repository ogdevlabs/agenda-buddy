namespace AgendaBuddy.EventAndCommands.Events.Profession;

public class AddProfessionsToProviderEvent : INotification
{
    public required string Email { get; set; }
    public required List<string> ProfessionNames { get; set; }
}
