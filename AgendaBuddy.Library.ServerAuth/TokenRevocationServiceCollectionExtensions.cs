using AgendaBuddy.Library.Configuration;
using AgendaBuddy.Library.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace AgendaBuddy.Library.Security;

public static class TokenRevocationServiceCollectionExtensions
{
    /// <summary>
    /// One registration shared verbatim by every service, since the denylist is cross-service by
    /// design — unlike each service's own per-service repositories.
    /// </summary>
    public static IServiceCollection AddTokenRevocationStore(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(serviceProvider =>
        {
            var databaseName = MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "agenda_buddy");
            var database = serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName);
            return new MongoTokenRevocationStore(database);
        });
        services.AddSingleton<ITokenRevocationStore>(sp => sp.GetRequiredService<MongoTokenRevocationStore>());

        return services;
    }
}
