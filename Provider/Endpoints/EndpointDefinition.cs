using EventAndCommands.Commands;
using Kafka;
using MediatR;
using Provider.Infrastructure.Data;
using Provider.Models;
using Provider.Requests;

namespace Provider.Endpoints;

public class EndpointDefinition: IEndpointDefinition
{
    private readonly IRequestCollection _requestCollection;
    
    public EndpointDefinition(IRequestCollection requestCollection)
    {
        _requestCollection = requestCollection;
    }

    public void RegisterEndpoints(WebApplication app)
    {

        app.MapPost("api/v1/providers", async(IMediator mediator, ProviderContext context, ProviderModel provider) =>
        {
            await context.Providers!.AddAsync(provider);
            await context.SaveChangesAsync();
            await _requestCollection.CreateTopicNotification(mediator, "WinniePoe");
            await Notify(mediator, "Provider:Created");
            
            return Results.Created($"api/v1/providers/{provider.Id}", provider);
        });
    }
    
    private static async Task<IResult> Notify(IMediator mediator, string message)
    {
        await new RequestHandler(mediator).Handle(new Request() { Message = message },
            new CancellationToken());
        return Results.Ok("Notified");
    }

    
}