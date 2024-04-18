using EventAndCommands.Commands;
using MediatR;

namespace Provider.Requests;

public static class RequestCollection
{
    public static async Task<IResult> CreateTopic(IMediator mediator, string topicName)
    {
        await Task.CompletedTask;
        return Results.Ok("Notified");
    }
    
    private static async Task<IResult> Notify(IMediator mediator, string message)
    {
        await new RequestHandler(mediator).Handle(new Request() { Message = message },
            new CancellationToken());
        return Results.Ok("Notified");
    }
}