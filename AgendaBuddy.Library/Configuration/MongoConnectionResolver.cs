using Microsoft.Extensions.Configuration;

namespace AgendaBuddy.Library.Configuration;

/// <summary>
/// Single source of truth for locating MongoDB configuration, whatever shape it arrives in.
/// Aspire injects <c>ConnectionStrings:mongodb</c>; the three legacy shapes are retained so a
/// service still starts when run standalone or from the pre-Aspire configuration files.
/// </summary>
public static class MongoConnectionResolver
{
    /// <summary>Connection-string keys in resolution order — first non-empty wins.</summary>
    private static readonly string[] ConnectionStringKeys =
    [
        "ConnectionStrings:mongodb",                // Aspire-injected (primary)
        "MongoDbSettings:ConnectionString",         // Identity's existing shape
        "MongoDB:ConnectionString",                 // legacy Development shape
        "LibrarySettings:MongoDB:ConnectionString"  // legacy appsettings.json shape
    ];

    /// <summary>Prefixes searched for named settings, in the same order of preference.</summary>
    private static readonly string[] SettingPrefixes =
    [
        "MongoDbSettings",
        "MongoDB",
        "LibrarySettings:MongoDB"
    ];

    /// <summary>
    /// Resolves the MongoDB connection string.
    /// </summary>
    /// <param name="configuration">The configuration to search.</param>
    /// <returns>The first non-empty connection string found.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no key yields a value. The message names every key tried and what to set,
    /// so the failure is actionable rather than a downstream null-argument throw.
    /// </exception>
    public static string Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        foreach (var key in ConnectionStringKeys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        throw new InvalidOperationException(
            "No MongoDB connection string found. Set one of: " +
            string.Join(", ", ConnectionStringKeys) +
            ". When running under AgendaBuddy.AppHost this is injected automatically; " +
            "to run this service standalone set ConnectionStrings__mongodb.");
    }

    /// <summary>
    /// Resolves a named setting — a database or collection name — with the same fallback
    /// discipline as <see cref="Resolve"/>.
    /// </summary>
    /// <param name="configuration">The configuration to search.</param>
    /// <param name="name">
    /// The setting name, supplied per call. Services do not share one convention: Identity
    /// reads <c>CollectionName</c> while the domain services read per-entity names such as
    /// <c>ProvidersCollection</c>.
    /// </param>
    /// <param name="default">Returned when no prefix yields a value.</param>
    /// <returns>The configured value, or <paramref name="default"/>.</returns>
    public static string ResolveSetting(IConfiguration configuration, string name, string @default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (var prefix in SettingPrefixes)
        {
            var value = configuration[$"{prefix}:{name}"];
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return @default;
    }
}
