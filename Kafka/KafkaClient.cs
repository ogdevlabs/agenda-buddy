using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;

namespace Kafka;

/// <summary>
/// Creates the per-provider Kafka topics.
/// </summary>
/// <param name="configuration">
/// Optional so <c>new KafkaClient()</c> stays valid for the existing registrations and tests.
/// When resolved from DI the container supplies it, so <c>AddSingleton&lt;IKafkaClient,
/// KafkaClient&gt;()</c> becomes configuration-driven without touching any <c>Program.cs</c>.
/// </param>
public class KafkaClient(IConfiguration? configuration = null) : IKafkaClient
{
    /// <summary>Used when no configuration supplies an address — the historical behaviour.</summary>
    internal const string DefaultBootstrapServers = "localhost:9092";

    /// <summary>
    /// Broker address, resolved once at construction. <c>internal</c> so the address can be
    /// asserted without a live broker; see <c>InternalsVisibleTo</c> in Kafka.csproj.
    /// </summary>
    internal string BootstrapServers { get; } = Resolve(configuration);

    /// <summary>
    /// Resolves the broker address: the Aspire-injected connection string first, then the
    /// appsettings key, then the local default.
    /// </summary>
    /// <remarks>
    /// Deliberately not sharing <c>Library.Configuration.MongoConnectionResolver</c>: coupling
    /// this project to <c>Library</c> for a two-key lookup would cost more than it saves.
    /// </remarks>
    private static string Resolve(IConfiguration? configuration)
    {
        if (configuration is null) return DefaultBootstrapServers;

        foreach (var key in new[] { "ConnectionStrings:kafka", "Kafka:BootstrapServers" })
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return DefaultBootstrapServers;
    }

    /// <summary>
    /// Creates <paramref name="topicName"/> unless it already exists.
    /// </summary>
    /// <param name="topicName">The topic to create.</param>
    /// <returns>A human-readable description of the outcome.</returns>
    public async Task<string> CreateTopicIfNotExist(string topicName)
    {
        var config = new AdminClientConfig
        {
            BootstrapServers = BootstrapServers
        };

        using var adminClient = new AdminClientBuilder(config).Build();

        try
        {
            var topic = new TopicSpecification
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1
            };
            await adminClient.CreateTopicsAsync(new[] { topic }, new CreateTopicsOptions
            {
                OperationTimeout = TimeSpan.FromSeconds(5),
                RequestTimeout = TimeSpan.FromSeconds(10)
            });

            return $"Topic '{topicName}' created successfully";
        }
        catch (CreateTopicsException e)
        {
            if (e.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
                return $"Exception Topic '{topicName}' already exists.";
        }
        catch (Exception e)
        {
            return $"Exception: {e.Message}";
        }

        return string.Empty;
    }
}
