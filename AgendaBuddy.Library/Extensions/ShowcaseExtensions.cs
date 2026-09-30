using AgendaBuddy.Library.Configuration;
using AgendaBuddy.Library.Media;
using AgendaBuddy.Library.Showcase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgendaBuddy.Library.Extensions;

public static class ShowcaseExtensions
{
    public const string InMemoryMediaKey = "Showcase:Media:InMemory";
    public const string MediaConnectionName = "media";
    public const string MediaContainerKey = "Showcase:Media:Container";

    /// <summary>
    /// Registers the showcase and media services, their six collections and the blob store.
    /// </summary>
    /// <remarks>
    /// The blob store is chosen on configuration, not on the environment name (ADR-033):
    /// <c>Showcase:Media:InMemory=true</c> for tests, <c>ConnectionStrings:media</c> (a blob service URI, which
    /// authenticates with the managed identity, or an emulator connection string) for everything else. With
    /// neither, uploads and fetches answer 503 naming the missing key, and nothing else in the service is affected.
    /// </remarks>
    public static IServiceCollection AddShowcase(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseName = MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "agenda_buddy");

        services.TryAddShowcaseCollections(configuration, databaseName);
        services.TryAddCollection<ProviderEntity>(configuration, "ProvidersCollection", "providers", databaseName);
        services.TryAddCollection<CustomerEntity>(configuration, "CustomersCollection", "customers", databaseName);
        services.TryAddCollection<AppointmentEntity>(configuration, "AppointmentsCollection", "appointments", databaseName);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ImagePipeline>();
        services.TryAddSingleton(ResolveBlobStore(configuration));

        services.TryAddScoped<IShowcaseService, ShowcaseService>();
        services.TryAddScoped<IMediaService, MediaService>();
        services.TryAddScoped<MediaSweeper>();

        return services;
    }

    private static IBlobStore ResolveBlobStore(IConfiguration configuration)
    {
        if (configuration.GetValue<bool>(InMemoryMediaKey))
            return new InMemoryBlobStore();

        var connection = configuration.GetConnectionString(MediaConnectionName);
        if (string.IsNullOrWhiteSpace(connection))
            return new UnavailableBlobStore();

        var container = configuration[MediaContainerKey];
        return AzureBlobStore.FromConnection(connection, string.IsNullOrWhiteSpace(container) ? "media" : container);
    }
}
