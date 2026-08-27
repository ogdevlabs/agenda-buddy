using AgendaBuddy.Kafka;

namespace AgendaBuddy.IntegrationTests.Support;

/// <summary>
/// Records every topic-creation call instead of talking to a real Kafka broker.
/// </summary>
/// <remarks>
/// F-018-T10 / ADR-017: Kafka is not containerised in this suite — it only ever creates topics, nothing
/// is produced or consumed, so a real broker would be the slowest container in the suite for proving
/// almost nothing. <see cref="IKafkaClient"/> is registered as a DI singleton in every service that uses
/// it, so it swaps cleanly via <c>WebApplicationFactory.ConfigureTestServices</c>. This fake still returns
/// the exact success-string shape <see cref="KafkaClient.CreateTopicIfNotExist"/> returns on success, so
/// the string-sniffing contract callers depend on (<c>!result.ToLower().StartsWith("exception")</c>) stays
/// guarded rather than short-circuited.
/// </remarks>
public sealed class KafkaClientFake : IKafkaClient
{
    private readonly List<string> _createdTopics = [];

    /// <summary>Every topic name this fake was asked to create, in call order.</summary>
    public IReadOnlyList<string> CreatedTopics => _createdTopics;

    public Task<string> CreateTopicIfNotExist(string topicName)
    {
        _createdTopics.Add(topicName);
        return Task.FromResult($"Topic '{topicName}' created successfully");
    }
}
