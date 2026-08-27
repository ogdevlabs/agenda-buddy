using AgendaBuddy.EventAndCommands.Persistence;

namespace AgendaBuddy.EventAndCommands;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventStore(this IServiceCollection services)
    {
        // Registered here rather than in each service's Program.cs so no consumer has to remember it:
        // EventStore needs the calling principal to stamp Event.Actor (F-016-T18 / ADR-027), and every
        // consumer of AddEventStore is an ASP.NET Core application. Idempotent.
        services.AddHttpContextAccessor();
        services.AddScoped<IEventStore, EventStore>();
        return services;
    }
}
