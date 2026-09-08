#nullable enable
using System.Linq;
using AgendaBuddy.Calendar.Extensions;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace AgendaBuddy.Calendar.Tests.Extensions;

/// <summary>
/// R-3 guards for Calendar: one of seven services being left on the old configuration path is the
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
        Assert.NotNull(scope.ServiceProvider.GetService<IRepository<CalendarBlockEntity>>());
    }

    /// <summary>
    /// <c>CheckCalendarAvailabilityQueryHandler</c> takes this, and the calculator's blocks argument is optional —
    /// so a missing registration is a startup failure rather than a compile error, and the shape it would
    /// degrade to is "offer the customer slots inside the provider's time off".
    /// </summary>
    [Fact]
    public void AddMongoDbRepository_RegistersTheTimeOffBlockService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMongoClient>(new MongoClient("mongodb://localhost:27017"));

        services.AddMongoDbRepository(AspireOnly());

        var descriptor = Assert.Single(
            services.Where(service => service.ServiceType == typeof(ICalendarBlockService)));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    // Every repository this service registers is scoped, and the count is asserted so an accidental extra
    // registration -- or a lost one -- is visible rather than silent.
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

        // Providers, appointments, customers, and calendar blocks -- time off is its own collection, not
        // whole-day fake appointments in `appointments`.
        Assert.Equal(4, repositories.Count);
        Assert.All(repositories, descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
    }
}
