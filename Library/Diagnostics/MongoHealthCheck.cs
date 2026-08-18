using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Library.Diagnostics;

/// <summary>
/// Readiness probe for MongoDB. Issues <c>{ ping: 1 }</c> against the <c>admin</c> database
/// through the injected client, so it reports on the same connection pool the service actually
/// uses rather than opening its own.
/// </summary>
/// <remarks>
/// Results are cached for <see cref="CacheWindow"/>. <c>/health</c> is anonymous, so without a
/// throttle any caller could drive one database round-trip per request (threat T-002). The
/// double-checked semaphore mirrors the existing <c>CacheAside</c> pattern in this codebase.
/// Register as a singleton — a per-request instance caches nothing.
/// </remarks>
public class MongoHealthCheck : IHealthCheck
{
    /// <summary>How long a probe result is reused before MongoDB is contacted again.</summary>
    private static readonly TimeSpan CacheWindow = TimeSpan.FromSeconds(5);

    private readonly IMongoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private HealthCheckResult? _cached;
    private DateTimeOffset _cachedAt;

    /// <summary>
    /// Creates the health check.
    /// </summary>
    /// <param name="client">The process-wide client, injected so no second pool is opened.</param>
    /// <param name="timeProvider">
    /// Clock used for the cache window. Defaults to <see cref="TimeProvider.System"/>; tests
    /// supply a controllable one so the window is asserted by call count rather than by sleeping.
    /// </param>
    public MongoHealthCheck(IMongoClient client, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Reports whether MongoDB is reachable, reusing a recent result when one is available.
    /// </summary>
    /// <param name="context">The health-check context supplied by the framework.</param>
    /// <param name="cancellationToken">Token that aborts the probe.</param>
    /// <returns>
    /// <see cref="HealthStatus.Healthy"/> when the ping succeeds, otherwise
    /// <see cref="HealthStatus.Unhealthy"/> carrying the failure. Never throws for an
    /// unreachable database — a probe that throws tells the orchestrator nothing.
    /// </returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (TryGetCached(out var cached)) return cached;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Re-check inside the gate: a concurrent probe may have refreshed the result while
            // this one waited.
            if (TryGetCached(out cached)) return cached;

            var result = await ProbeAsync(cancellationToken);

            _cached = result;
            _cachedAt = _timeProvider.GetUtcNow();

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Returns a cached result while it is still inside the window. Unhealthy results expire on
    /// the same schedule, so a recovered database is reported healthy at the next window.
    /// </summary>
    private bool TryGetCached(out HealthCheckResult result)
    {
        var cached = _cached;
        if (cached.HasValue && _timeProvider.GetUtcNow() - _cachedAt < CacheWindow)
        {
            result = cached.Value;
            return true;
        }

        result = default;
        return false;
    }

    private async Task<HealthCheckResult> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(
                    new BsonDocument("ping", 1),
                    cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy("MongoDB responded to ping.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("MongoDB did not respond to ping.", exception);
        }
    }
}
