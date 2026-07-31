using Library.Entities;
using Library.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Library.Tools.Migrations;

/// <summary>
/// One-time migration: seeds a CredentialEntity for every existing Provider and Customer.
/// Skipped records (duplicate email) are logged and counted — never fail-fast.
/// MustResetPassword=true so migrated users reset on first login.
/// </summary>
public static class SeedAuthCredentials
{
    public static async Task<MigrationResult> RunAsync(
        IRepository<ProviderEntity> providers,
        IRepository<CustomerEntity> customers,
        IRepository<CredentialEntity> credentials,
        ILogger logger)
    {
        var inserted = 0;
        var skipped = 0;

        var allProviders = (await providers.GetAllAsync()).ToList();
        foreach (var provider in allProviders)
        {
            if (string.IsNullOrWhiteSpace(provider.Email)) { skipped++; continue; }

            var email = provider.Email.ToLowerInvariant();
            var result = await TryInsertCredential(credentials, email, "Provider", logger);
            if (result) inserted++; else skipped++;
        }

        var allCustomers = (await customers.GetAllAsync()).ToList();
        foreach (var customer in allCustomers)
        {
            if (string.IsNullOrWhiteSpace(customer.Email)) { skipped++; continue; }

            var email = customer.Email.ToLowerInvariant();
            var result = await TryInsertCredential(credentials, email, "Customer", logger);
            if (result) inserted++; else skipped++;
        }

        logger.LogInformation("SeedAuthCredentials complete: inserted={Inserted}, skipped={Skipped}",
            inserted, skipped);

        return new MigrationResult(inserted, skipped);
    }

    private static async Task<bool> TryInsertCredential(
        IRepository<CredentialEntity> credentials,
        string email,
        string role,
        ILogger logger)
    {
        try
        {
            // Generate a random, unusable password hash — user must reset on first login
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), workFactor: 12);

            var credential = new CredentialEntity
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Email = email,
                PasswordHash = passwordHash,
                Role = role,
                MustResetPassword = true,
                RefreshToken = null
            };

            await credentials.InsertAsync(credential);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            logger.LogDebug("SeedAuthCredentials: skipping duplicate email={Email}", email);
            return false;
        }
    }
}

public record MigrationResult(int Inserted, int Skipped);
