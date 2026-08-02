using EventAndCommands.Persitency;

namespace EventAndCommands;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventStore(this IServiceCollection services)
    {
        services.AddScoped<IEventStore, EventStore>();
        return services;
    }
}
