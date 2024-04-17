using EventAndCommands.Commands;
using MediatR;
using Provider.Infrastructure.Data;
using Provider.Models;

namespace Provider.Endpoints;

public class EndpointDefinition: IEndpointDefinition
{
    public void RegisterEndpoints(WebApplication app)
    {

        app.MapPost("api/v1/providers", async(IMediator mediator, ProviderContext context, ProviderModel provider) =>
        {
            await context.Providers!.AddAsync(provider);
            await context.SaveChangesAsync();
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