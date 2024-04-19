using Provider.Endpoints;
using Provider.Requests;

namespace Provider.Extensions;

public static class MinimalApi
{
    public static void RegisterEndpoints(this WebApplication app)
    {
        
        var endpointDefinitions = typeof(Program).Assembly
            .GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IEndpointDefinition)) && !t.IsAbstract && !t.IsInterface)
            .Select(Activator.CreateInstance)
            .Cast<IEndpointDefinition>();

        foreach (var endpoint in endpointDefinitions)
        {
            endpoint.RegisterEndpoints(app);
        }
    }
}