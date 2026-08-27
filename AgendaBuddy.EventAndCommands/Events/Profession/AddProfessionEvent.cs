namespace AgendaBuddy.EventAndCommands.Events.Profession;

public class AddProfessionEvent : INotification
{
    public ProfessionEntity? ProfessionEntity { get; set; }
}
