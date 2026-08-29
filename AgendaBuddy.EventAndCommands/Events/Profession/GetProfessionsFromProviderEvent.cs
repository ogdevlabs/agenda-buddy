namespace AgendaBuddy.EventAndCommands.Events.Profession;

public class GetProfessionsFromProviderEvent : INotification
{
    public required string Email { get; set; }
}
