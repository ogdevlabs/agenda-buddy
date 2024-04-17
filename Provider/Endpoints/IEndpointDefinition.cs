namespace Provider.Endpoints;

public interface IEndpointDefinition
{
    void RegisterEndpoints(WebApplication app);
}