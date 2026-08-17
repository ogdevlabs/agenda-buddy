using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Library.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Common.Tests.Diagnostics;

[TestSubject(typeof(MongoHealthCheck))]
public class MongoHealthCheckTest
{
    /// <summary>
    /// Advances only when told to. The cache window is asserted by call count, never by
    /// sleeping — wall-clock assertions are what made AvailabilityScheduleTest fragile.
    /// </summary>
    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private static readonly DateTimeOffset Start = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private static (Mock<IMongoClient> Client, Mock<IMongoDatabase> Database) MongoMocks()
    {
        var database = new Mock<IMongoDatabase>();
        var client = new Mock<IMongoClient>();
        client.Setup(c => c.GetDatabase("admin", null)).Returns(database.Object);
        return (client, database);
    }

    private static void SetupPing(Mock<IMongoDatabase> database, bool succeeds)
    {
        var setup = database.Setup(d => d.RunCommandAsync(
            It.IsAny<Command<BsonDocument>>(),
            It.IsAny<ReadPreference>(),
            It.IsAny<CancellationToken>()));

        if (succeeds) setup.ReturnsAsync(new BsonDocument("ok", 1));
        else setup.ThrowsAsync(new TimeoutException("no reachable servers"));
    }

    // Given MongoDB answers the ping, When /health is probed, Then the check is healthy.
    [Fact]
    public async Task CheckHealth_IsHealthy_WhenPingSucceeds()
    {
        var (client, database) = MongoMocks();
        SetupPing(database, succeeds: true);
        var check = new MongoHealthCheck(client.Object, new ManualTimeProvider(Start));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    // AC-3.2: Given MongoDB is unreachable, When /health is probed, Then the check reports
    // unhealthy rather than throwing out of the probe.
    [Fact]
    public async Task CheckHealth_IsUnhealthy_WhenPingThrows()
    {
        var (client, database) = MongoMocks();
        SetupPing(database, succeeds: false);
        var check = new MongoHealthCheck(client.Object, new ManualTimeProvider(Start));

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    // Threat T-002: an anonymous caller must not be able to drive one Mongo round-trip per
    // request. Two probes inside the window produce exactly one ping.
    [Fact]
    public async Task CheckHealth_PingsOnce_ForRepeatedProbesInsideTheCacheWindow()
    {
        var (client, database) = MongoMocks();
        SetupPing(database, succeeds: true);
        var time = new ManualTimeProvider(Start);
        var check = new MongoHealthCheck(client.Object, time);

        await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(4));
        var second = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, second.Status);
        database.Verify(d => d.RunCommandAsync(
            It.IsAny<Command<BsonDocument>>(),
            It.IsAny<ReadPreference>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // The cache is a throttle, not a freeze — once the window lapses the database is re-probed.
    [Fact]
    public async Task CheckHealth_PingsAgain_OnceTheCacheWindowLapses()
    {
        var (client, database) = MongoMocks();
        SetupPing(database, succeeds: true);
        var time = new ManualTimeProvider(Start);
        var check = new MongoHealthCheck(client.Object, time);

        await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(6));
        await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        database.Verify(d => d.RunCommandAsync(
            It.IsAny<Command<BsonDocument>>(),
            It.IsAny<ReadPreference>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // Standup rule: a cached *unhealthy* result must expire too, or a recovered database keeps
    // reporting unhealthy for as long as traffic continues.
    [Fact]
    public async Task CheckHealth_RecoversAfterTheWindow_WhenMongoComesBack()
    {
        var (client, database) = MongoMocks();
        SetupPing(database, succeeds: false);
        var time = new ManualTimeProvider(Start);
        var check = new MongoHealthCheck(client.Object, time);

        var down = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        Assert.Equal(HealthStatus.Unhealthy, down.Status);

        SetupPing(database, succeeds: true);
        time.Advance(TimeSpan.FromSeconds(6));
        var recovered = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, recovered.Status);
    }

    // An unhealthy result is cached as well, so a down database is not hammered either.
    [Fact]
    public async Task CheckHealth_CachesUnhealthyResult_InsideTheWindow()
    {
        var (client, database) = MongoMocks();
        SetupPing(database, succeeds: false);
        var time = new ManualTimeProvider(Start);
        var check = new MongoHealthCheck(client.Object, time);

        await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(2));
        var second = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, second.Status);
        database.Verify(d => d.RunCommandAsync(
            It.IsAny<Command<BsonDocument>>(),
            It.IsAny<ReadPreference>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Defaulting to TimeProvider.System keeps the production registration a one-liner.
    [Fact]
    public async Task CheckHealth_UsesSystemTime_WhenNoTimeProviderIsSupplied()
    {
        var (client, database) = MongoMocks();
        SetupPing(database, succeeds: true);
        var check = new MongoHealthCheck(client.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
