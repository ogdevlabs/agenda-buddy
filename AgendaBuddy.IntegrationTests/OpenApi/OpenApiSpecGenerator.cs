using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace AgendaBuddy.IntegrationTests.OpenApi;

/// <summary>
/// Generates a byte-deterministic OpenAPI v3 JSON document for a service, straight from its own
/// <see cref="ISwaggerProvider"/> (F-018-T16, AC-17/AC-18).
/// </summary>
/// <remarks>
/// <para>
/// <b>Spike-proven mechanism.</b> <c>AddSwaggerGen()</c> is registered unconditionally in every
/// service's <c>Program.cs</c> — only <c>UseSwagger()</c>/<c>UseSwaggerUI()</c> are gated behind
/// <c>IsDevelopment()</c>. So booting the host through a plain
/// <see cref="WebApplicationFactory{TEntryPoint}"/> and resolving <see cref="ISwaggerProvider"/> from
/// its own DI container reaches the exact document the Development-only HTTP endpoint would have
/// served — no HTTP request, no environment override, no new NuGet package.
/// </para>
/// <para>
/// <b>No container needed.</b> None of the seven services touch MongoDB at DI-registration time — the
/// <c>IMongoClient</c> singleton is a deferred factory (constructing a <c>MongoClient</c> parses a
/// connection string; it does not dial). Profession is the one exception:
/// <c>ProfessionSeedHostedService</c> resolves <c>IMongoClient</c> at host-start and queries it, so
/// every service is given an unreachable-but-syntactically-valid connection string — cheap insurance,
/// and genuinely unreachable rather than a real database, matching this task's own framing. Profession's
/// hosted service swallows the resulting failure by design (it must start even with no database).
/// </para>
/// </remarks>
public static class OpenApiSpecGenerator
{
    private const string UnreachableMongoConnectionString =
        "mongodb://127.0.0.1:1/?connectTimeoutMS=200&serverSelectionTimeoutMS=200";

    /// <summary>
    /// Boots <typeparamref name="TEntryPoint"/>'s host and returns its "v1" OpenAPI document as
    /// byte-deterministic JSON.
    /// </summary>
    /// <remarks>
    /// AC-18: throws — and writes nothing — if the host cannot boot or has no "v1" document. Callers
    /// must not catch this and must not write a file until this method returns.
    /// </remarks>
    public static string Generate<TEntryPoint>(string jwtPublicKeyPem) where TEntryPoint : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jwtPublicKeyPem);

        // AuthenticationExtensions.AddAgendaBuddyAuthentication() reads this eagerly, at
        // DI-registration time, before the factory below builds anything — it has to be set first.
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", jwtPublicKeyPem);

        using var factory = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:mongodb", UnreachableMongoConnectionString));

        // A fresh scope rather than the root provider: ISwaggerProvider's own lifetime is Swashbuckle's
        // choice, not this project's, so resolving from a scope is correct regardless of it.
        using var scope = factory.Services.CreateScope();
        var document = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        return Serialize(document);
    }

    /// <summary>
    /// Pinned writer settings (AC-17's open item): explicit rather than the package defaults, so a
    /// future Microsoft.OpenApi upgrade changing its own defaults cannot silently change this
    /// project's committed baseline. Key/property order comes from the document model itself — the
    /// same model the Development-only HTTP endpoint would have serialized.
    /// </summary>
    private static string Serialize(Microsoft.OpenApi.OpenApiDocument document)
    {
        var stringWriter = new StringWriter { NewLine = "\n" };
        var settings = new OpenApiJsonWriterSettings
        {
            Terse = false,
            InlineLocalReferences = false,
            InlineExternalReferences = false,
        };
        var jsonWriter = new OpenApiJsonWriter(stringWriter, settings);

        document.SerializeAsV3(jsonWriter);

        return stringWriter.ToString();
    }
}
