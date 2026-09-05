using AgendaBuddy.Library.Configuration;

namespace AgendaBuddy.Booking.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtension
{
    /// <summary>
    /// Registers the repositories for this service against the shared <see cref="IMongoClient"/>.
    /// </summary>
    /// <remarks>
    /// The client is resolved from the provider per registration rather than constructed here, so
    /// this method no longer opens a connection pool of its own (AC-4.3) and no longer depends on
    /// a connection string being present in configuration (AC-4.1). Names come from
    /// <see cref="MongoConnectionResolver"/>, so the Aspire-injected shape and every legacy shape
    /// resolve identically (R-3).
    /// </remarks>
    public static IServiceCollection AddMongoDbRepository(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        var databaseName = MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "agenda_buddy");
        var providersCollection = MongoConnectionResolver.ResolveSetting(configuration, "ProvidersCollection", "providers");
        var appointmentsCollection = MongoConnectionResolver.ResolveSetting(configuration, "AppointmentsCollection", "appointments");
        var customersCollection = MongoConnectionResolver.ResolveSetting(configuration, "CustomersCollection", "customers");
        serviceCollection.AddScoped<IRepository<ProviderEntity>>(serviceProvider =>
            new MongoDbRepository<ProviderEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                providersCollection));

        serviceCollection.AddScoped<IRepository<AppointmentEntity>>(serviceProvider =>
            new MongoDbRepository<AppointmentEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                appointmentsCollection));

        serviceCollection.AddScoped<IRepository<CustomerEntity>>(serviceProvider =>
            new MongoDbRepository<CustomerEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                customersCollection));

        // Notes and payments were never persisted before this, because nothing registered a repository
        // for them — MongoDB creates each collection on first write, so there is no migration and no
        // provisioning step.
        var notesCollection = MongoConnectionResolver.ResolveSetting(configuration, "NotesCollection", "notes");
        var paymentsCollection = MongoConnectionResolver.ResolveSetting(configuration, "PaymentsCollection", "payments");

        serviceCollection.AddScoped<IRepository<NoteEntity>>(serviceProvider =>
            new MongoDbRepository<NoteEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                notesCollection));

        serviceCollection.AddScoped<IRepository<PaymentEntity>>(serviceProvider =>
            new MongoDbRepository<PaymentEntity>(
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
                paymentsCollection));

        // Booking is where appointments are requested, accepted, completed and cancelled, so it is where
        // the other party has to be told — by email and push as well as in the inbox, because the inbox only
        // reaches somebody who is already in the app. The notifications collection name matches Customer's,
        // which owns the read side (GET /api/v1/notifications) — both point at the same documents deliberately.
        serviceCollection.AddNotificationDelivery(configuration);

        serviceCollection.AddScoped<ProviderService>();
        serviceCollection.AddScoped<BookingService>();
        serviceCollection.AddScoped<CustomerService>();

        // Party Review: Update/CancelAppointmentCommandHandler take IProviderService/IBookingService
        // (Echo's finding -- both interfaces already cover everything those two handlers call, unlike
        // Book's, which still needs AppendAppointmentAsync). Forwarding to the already-scoped concrete
        // instance, not a second AddScoped<IProviderService, ProviderService>, so a request that resolves
        // both the concrete class (route handlers) and the interface (command handlers) in the same scope
        // gets the same object, not two.
        serviceCollection.AddScoped<IProviderService>(sp => sp.GetRequiredService<ProviderService>());
        serviceCollection.AddScoped<IBookingService>(sp => sp.GetRequiredService<BookingService>());

        // Interface-typed, unlike the four above: nothing in Booking needs the concrete classes, and both
        // services take only their repository plus (for payments) the gateway.
        serviceCollection.AddScoped<INoteService, NoteService>();
        serviceCollection.AddScoped<IPaymentService, PaymentService>();

        // NON-CHARGING unless a Stripe key is configured, and the key is
        // never in appsettings.json — it is an Aspire secret parameter, as the JWT keys are. Singleton because
        // StripePaymentGateway assigns the process-global StripeConfiguration.ApiKey once at construction.
        serviceCollection.AddSingleton<IPaymentGateway>(_ => PaymentGatewayFactory.Create(configuration));

        serviceCollection.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        return serviceCollection;
    }
}
