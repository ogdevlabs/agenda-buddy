using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.Library.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Persistence;

/// <summary>
/// Resolves a hosted service's own collection name through <see cref="MongoConnectionResolver"/> instead
/// of a literal string (F-018-T12). Every domain service resolves its collection name the same way at
/// startup (see e.g. <c>Booking/Extensions/ServiceCollectionExtension.cs</c>); a test that hardcoded
/// "providers" instead would keep passing even if that resolution ever changed, which defeats the point
/// of proving persistence against the service's REAL configuration.
/// </summary>
internal static class ConfiguredCollection
{
    /// <summary>
    /// The collection this <paramref name="service"/> instance actually reads and writes for
    /// <typeparamref name="TEntity"/> — same setting name and default the service's own
    /// <c>ServiceCollectionExtension</c> resolves.
    /// </summary>
    public static IMongoCollection<TEntity> Of<TEntity>(
        ServiceHost service, string settingName, string @default)
    {
        var configuration = service.Services.GetRequiredService<IConfiguration>();
        var name = MongoConnectionResolver.ResolveSetting(configuration, settingName, @default);
        return service.Database.GetCollection<TEntity>(name);
    }
}
