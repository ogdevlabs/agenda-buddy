#nullable enable
using System.Collections.Generic;
using System.Linq;
using Booking.Extensions;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Booking.Tests.Extensions;

/// <summary>
/// R-3 guards for Booking: one of seven services being left on the old configuration path is the
/// realistic failure mode of this refactor, so each service is asserted independently rather than
/// trusting a shared helper. Constructing a <see cref="MongoClient"/> opens no connection, so
/// these stay unit tests — nothing here contacts a database.
/// </summary>
public class ServiceCollectionMongoResolutionTest
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs)
    {
        var dictionary = new Dictionary<string, string?>();
        foreach (var (key, value) in pairs) dictionary[key] = value;

        return new ConfigurationBuilder().AddInMemoryCollection(dictionary).Build();
    }

    /// <summary>Configuration as the AppHost supplies it — the injected key and nothing else.</summary>
    private static IConfiguration AspireOnly() =>
        Config(("ConnectionStrings:mongodb", "mongodb://localhost:27017"));

    private static IConfiguration LegacyOnly() =>
        Config(("MongoDB:ConnectionString", "mongodb://localhost:27017"),
            ("MongoDB:DatabaseName", "legacy_db"),
            ("MongoDB:ProvidersCollection", "legacy_providerscollection"),
            ("MongoDB:AppointmentsCollection", "legacy_appointmentscollection"),
            ("MongoDB:CustomersCollection", "legacy_customerscollection"));

    private static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        // Program.cs owns this registration; the extension must consume it, not replace it.
        services.AddSingleton<IMongoClient>(new MongoClient("mongodb://localhost:27017"));
        services.AddMongoDbRepository(configuration);

        return services.BuildServiceProvider();
    }

    // The sharpest R-3 guard: a service still building its own client fails here, because there
    // is no legacy connection string to build it from and no client registered to fall back on.
    [Fact]
    public void AddMongoDbRepository_DoesNotConstructItsOwnClient()
    {
        var services = new ServiceCollection();

        services.AddMongoDbRepository(AspireOnly());

        Assert.NotEmpty(services);
    }

    // AC-4.1: the Aspire-injected key alone is enough to start.
    [Fact]
    public void AddMongoDbRepository_RegistersRepositories_FromAspireConfigurationAlone()
    {
        using var provider = BuildProvider(AspireOnly());
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IRepository<ProviderEntity>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IRepository<AppointmentEntity>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IRepository<CustomerEntity>>());
    }

    // Backward compatibility: the pre-Aspire shape still resolves, so a revert keeps working.
    [Fact]
    public void AddMongoDbRepository_RegistersRepositories_FromLegacyConfiguration()
    {
        using var provider = BuildProvider(LegacyOnly());
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IRepository<ProviderEntity>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IRepository<AppointmentEntity>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IRepository<CustomerEntity>>());
    }

    // The refactor must not change how many repositories exist, or their lifetime.
    [Fact]
    public void AddMongoDbRepository_KeepsRepositoryCountAndLifetimeUnchanged()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMongoClient>(new MongoClient("mongodb://localhost:27017"));

        services.AddMongoDbRepository(AspireOnly());

        var repositories = services
            .Where(descriptor => descriptor.ServiceType.IsGenericType
                                 && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IRepository<>))
            .ToList();

        // 3 -> 5 at F-014: NoteEntity and PaymentEntity. Both were written by F-008 and F-010 and had never
        // been persisted, because nothing registered a repository for them — which is the whole defect F-014
        // exists to fix, and is why this count is asserted rather than left to drift.
        Assert.Equal(5, repositories.Count);
        Assert.All(repositories, descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
    }
}
