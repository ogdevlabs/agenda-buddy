// Global using directives

global using Xunit;

// Service entry-point anchors for ServiceHostFixture<TEntryPoint>. Each is a distinct PUBLIC type from
// one service assembly — never `Program`, which is internal and ambiguous across all seven. See
// Harness/EntryPoints.cs for the full rationale. Added per service as its test classes arrive.
global using ProfessionAnchor = Profession.Configurations.MongoDbConfiguration;
global using CustomerAnchor = Customer.Configurations.MongoDbConfiguration;
global using ProviderAnchor = Provider.Configurations.MongoDbConfiguration;
global using CalendarAnchor = Calendar.Configurations.MongoDbConfiguration;
global using ServicesAnchor = Services.Configurations.MongoDbConfiguration;
global using BookingAnchor = Booking.Configuration.MongoDbConfiguration;   // NOTE: Configuration, SINGULAR — Booking is the odd one out
global using IdentityAnchor = Identity.Configurations.MongoDbConfiguration;  // F-021: the auth routes
