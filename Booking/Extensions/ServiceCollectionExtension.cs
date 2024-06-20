namespace Booking.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddMongoDbRepository(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        var client = new MongoDbConfiguration(configuration).MongoClient();
        var database = client.GetDatabase(configuration.GetSection("MongoDB")["DatabaseName"]);

        serviceCollection.AddScoped<IRepository<ProviderEntity>>(
            _ => new MongoDbRepository<ProviderEntity>(database,
                configuration.GetSection("MongoDB")["ProvidersName"]!));

        serviceCollection.AddScoped<IRepository<AppointmentEntity>>(
            _ => new MongoDbRepository<AppointmentEntity>(database,
                configuration.GetSection("MongoDB")["AppointmentsName"]!));

        serviceCollection.AddScoped<ProviderService>();
        serviceCollection.AddScoped<BookingService>();

        return serviceCollection;
    }
}