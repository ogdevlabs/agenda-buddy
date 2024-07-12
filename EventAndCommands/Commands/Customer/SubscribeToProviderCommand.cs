namespace EventAndCommands.Commands.Customer;

public class SubscribeToProviderCommand : IRequest<string>
{
    public required CustomerSubscribedToProviderEntity CustomerSubscribedToProviderEntity { get; set; }
}