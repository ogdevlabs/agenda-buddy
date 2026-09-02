namespace AgendaBuddy.EventAndCommands.Events.Profession;

public class RemoveProfessionFromProviderEvent : INotification
{
    public required string Email { get; set; }
    public required string ProfessionName { get; set; }
}
