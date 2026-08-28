using AgendaBuddy.IntegrationTests.Harness;
using AgendaBuddy.IntegrationTests.Persistence;
using AgendaBuddy.EventAndCommands.Persistence;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Audit;

/// <summary>
/// F-024. The events collection must self-expire and stay indexable, so an audit record — and
/// whatever entity data it carries — never outlives its retention window and is never scanned
/// without an index.
/// </summary>
[Collection(HarnessCollection.Name)]
public class EventStoreRetentionIndexTest(ServiceHostFixture<BookingAnchor> host)
    : IClassFixture<ServiceHostFixture<BookingAnchor>>
{
    [Fact]
    public async Task EnsureIndexAsync_CreatesATtlIndexOnTimestamp_AndASecondaryIndexOnType()
    {
        using var service = host.StartService("Production");

        using (var scope = service.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IEventStore>().EnsureIndexAsync();
        }

        var events = ConfiguredCollection.Of<Event>(service, "EventsCollection", "events");
        var indexes = await events.Indexes.List().ToListAsync();

        var timestampIndex = indexes.SingleOrDefault(i =>
            i["key"].AsBsonDocument.Contains("timestamp"));
        Assert.NotNull(timestampIndex);
        Assert.True(timestampIndex!.Contains("expireAfterSeconds"),
            "the timestamp index must be a TTL index, or an audit record never expires");
        Assert.Equal(TimeSpan.FromDays(400).TotalSeconds, timestampIndex["expireAfterSeconds"].ToDouble());

        var typeIndex = indexes.SingleOrDefault(i => i["key"].AsBsonDocument.Contains("type"));
        Assert.NotNull(typeIndex);
        Assert.False(typeIndex!.Contains("expireAfterSeconds"),
            "the type index is for investigation queries, not expiry");
    }

    [Fact]
    public async Task EnsureIndexAsync_IsIdempotent_CallingItTwiceDoesNotThrow()
    {
        using var service = host.StartService("Production");

        using var scope = service.Services.CreateScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        await eventStore.EnsureIndexAsync();
        await eventStore.EnsureIndexAsync();
    }
}
