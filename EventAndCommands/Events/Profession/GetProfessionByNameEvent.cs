namespace EventAndCommands.Events.Profession;

public class GetProfessionByNameEvent : INotification
{
    public required string Name { get; set; }
}