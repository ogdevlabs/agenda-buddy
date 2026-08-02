using Library.Data;
using Library.Entities;
using Library.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Library.Tools.Migrations;

/// <summary>
/// Development-only migration: seeds 3 providers (with services) and 3 customers
/// with known credentials for local testing. Skips if accounts already exist.
/// </summary>
public static class SeedDevelopmentAccounts
{
    public static async Task<MigrationResult> RunAsync(
        IRepository<ProviderEntity> providers,
        IRepository<CustomerEntity> customers,
        IRepository<CredentialEntity> credentials,
        ILogger logger)
    {
        var inserted = 0;
        var skipped = 0;

        var seedProviders = DevelopmentSeedData.Providers();
        foreach (var provider in seedProviders)
        {
            var existing = await providers.FindOneAsync(
                new BsonDocument("email", provider.Email));

            if (existing is not null)
            {
                logger.LogDebug("SeedDevelopmentAccounts: provider {Email} already exists, skipping", provider.Email);
                skipped++;
                continue;
            }

            await providers.InsertAsync(provider);
            await EnsureCredential(credentials, provider.Email, "Provider", logger);
            inserted++;
            logger.LogInformation("SeedDevelopmentAccounts: seeded provider {Name} <{Email}>",
                $"{provider.FirstName} {provider.LastName}", provider.Email);
        }

        var seedCustomers = DevelopmentSeedData.Customers();
        foreach (var customer in seedCustomers)
        {
            var existing = await customers.FindOneAsync(
                new BsonDocument("email", customer.Email));

            if (existing is not null)
            {
                logger.LogDebug("SeedDevelopmentAccounts: customer {Email} already exists, skipping", customer.Email);
                skipped++;
                continue;
            }

            await customers.InsertAsync(customer);
            await EnsureCredential(credentials, customer.Email!, "Customer", logger);
            inserted++;
            logger.LogInformation("SeedDevelopmentAccounts: seeded customer {Name} <{Email}>",
                $"{customer.FirstName} {customer.LastName}", customer.Email);
        }

        logger.LogInformation(
            "SeedDevelopmentAccounts complete: inserted={Inserted}, skipped={Skipped}",
            inserted, skipped);

        return new MigrationResult(inserted, skipped);
    }

    private static async Task EnsureCredential(
        IRepository<CredentialEntity> credentials,
        string email,
        string role,
        ILogger logger)
    {
        try
        {
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(DevelopmentSeedData.DefaultPassword, workFactor: 12);

            var credential = new CredentialEntity
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Email = email.ToLowerInvariant(),
                PasswordHash = passwordHash,
                Role = role,
                MustResetPassword = false,
                RefreshToken = null
            };

            await credentials.InsertAsync(credential);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            logger.LogDebug("SeedDevelopmentAccounts: credential for {Email} already exists", email);
        }
    }
}
