#nullable enable
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Kafka.Tests;

/// <summary>
/// AC-5.5 — the broker address must come from configuration. A new file rather than an addition
/// to <see cref="KafkaClientTest"/>, whose stub must stay untouched (AC-5.2).
/// </summary>
[TestSubject(typeof(KafkaClient))]
public class KafkaClientBootstrapServersTest
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs)
    {
        var dictionary = new Dictionary<string, string?>();
        foreach (var (key, value) in pairs) dictionary[key] = value;

        return new ConfigurationBuilder().AddInMemoryCollection(dictionary).Build();
    }

    // AC-5.2: the parameterless construction used by the existing registrations and tests must
    // keep working, and keep its old behaviour.
    [Fact]
    public void ParameterlessConstruction_StillDefaultsToLocalhost()
    {
        Assert.Equal("localhost:9092", new KafkaClient().BootstrapServers);
    }

    [Fact]
    public void DefaultsToLocalhost_WhenConfigurationHasNeitherKey()
    {
        Assert.Equal("localhost:9092", new KafkaClient(Config()).BootstrapServers);
    }

    // The AppHost injects ConnectionStrings:kafka, so that is the primary key.
    [Fact]
    public void ReadsAspireInjectedConnectionString()
    {
        var client = new KafkaClient(Config(("ConnectionStrings:kafka", "kafka:19092")));

        Assert.Equal("kafka:19092", client.BootstrapServers);
    }

    // CONSTITUTION §9 calls the hardcoded address out as blocking non-local deployment, so a
    // plain appsettings key has to work too.
    [Fact]
    public void ReadsAppSettingsKey()
    {
        var client = new KafkaClient(Config(("Kafka:BootstrapServers", "broker.internal:9092")));

        Assert.Equal("broker.internal:9092", client.BootstrapServers);
    }

    [Fact]
    public void PrefersAspireInjectedConnectionString_WhenBothArePresent()
    {
        var client = new KafkaClient(Config(
            ("ConnectionStrings:kafka", "kafka:19092"),
            ("Kafka:BootstrapServers", "broker.internal:9092")));

        Assert.Equal("kafka:19092", client.BootstrapServers);
    }

    [Fact]
    public void TreatsWhitespaceAsAbsent()
    {
        var client = new KafkaClient(Config(
            ("ConnectionStrings:kafka", "   "),
            ("Kafka:BootstrapServers", "broker.internal:9092")));

        Assert.Equal("broker.internal:9092", client.BootstrapServers);
    }
}
