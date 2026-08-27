// Global using directives

global using Xunit;

// Service entry-point anchors for ServiceHostFixture<TEntryPoint>. Each is a distinct PUBLIC type from
// one service assembly — never `Program`, which is internal and ambiguous across all seven. See
// Harness/EntryPoints.cs for the full rationale. Added per service as its test classes arrive.
global using ProfessionAnchor = AgendaBuddy.Profession.Configurations.MongoDbConfiguration;
global using CustomerAnchor = Customer.Configurations.MongoDbConfiguration;
global using ProviderAnchor = AgendaBuddy.Provider.Configurations.MongoDbConfiguration;
global using CalendarAnchor = AgendaBuddy.Calendar.Configurations.MongoDbConfiguration;
global using ServicesAnchor = AgendaBuddy.Services.Configurations.MongoDbConfiguration;
global using BookingAnchor = AgendaBuddy.Booking.Configuration.MongoDbConfiguration;   // NOTE: Configuration, SINGULAR — Booking is the odd one out
global using AgendaBuddy.Booking.Domain.Responses;
global using IdentityAnchor = AgendaBuddy.Identity.Configurations.MongoDbConfiguration;  // F-021: the auth routes
global using GatewayAnchor = AgendaBuddy.Gateway.GatewayAnchor;  // F-015-T01: no MongoDB config type to reuse, so this is a dedicated marker — see AgendaBuddy.Gateway/GatewayAnchor.cs
