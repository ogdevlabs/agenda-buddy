using Library.Entities;
using Library.Repositories;
using Provider.Configurations;
using ProviderService = Library.Services.ProviderService;

namespace Provider.Extensions;


public static class ServiceCollectionExtension
{
    // public static IServiceCollection AddHealthChecks(this IServiceCollection serviceCollection,
    //     IConfiguration configuration)
    // {
    //     var healthCheckBuilder = serviceCollection.AddHealthChecks();
    //
    //     healthCheckBuilder
    //         .AddNpgSql(_ => configuration!.GetConnectionString("ProviderDB")!,
    //             name: "ProviderDB-Check",
    //             tags: new string[] { "ready" });
    //     
    //     return serviceCollection;
    // }
    //
    // public static IServiceCollection AddDbContexts(this IServiceCollection serviceCollection, 
    //     IConfiguration configuration)
    // {
    //     static void ConfigurePostgreSqlOptions(NpgsqlDbContextOptionsBuilder options)
    //     {
    //         options.MigrationsAssembly(typeof(Program).Assembly.FullName);
    //         options.EnableRetryOnFailure(maxRetryCount: 15);
    //     };
    //     
    //     serviceCollection.AddDbContext<ProviderContext>(options =>
    //     {
    //         var connectionString = configuration.GetConnectionString("ProviderDB");
    //
    //         options.UseNpgsql(connectionString, ConfigurePostgreSqlOptions);
    //     });
    //     
    //
    //     return serviceCollection;
    // }

    public static IServiceCollection AddMongoDbRepository(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        var client = new MongoDbConfiguration(configuration).MongoClient();
        var database = client.GetDatabase(configuration.GetSection("MongoDB")["DatabaseName"]);
        
        serviceCollection.AddScoped<IRepository<ProviderEntity>>(
            provider => new MongoDbRepository<ProviderEntity>(database, 
                configuration.GetSection("MongoDB")["CollectionName"]!));

        serviceCollection.AddScoped<ProviderService>();

        return serviceCollection;
    }
}