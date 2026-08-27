using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using AgendaBuddy.Library.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Common.Tests.Configuration;

[TestSubject(typeof(MongoConnectionResolver))]
public class MongoConnectionResolverTest
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs)
    {
        var dictionary = new Dictionary<string, string?>();
        foreach (var (key, value) in pairs) dictionary[key] = value;

        return new ConfigurationBuilder().AddInMemoryCollection(dictionary).Build();
    }

    // Given a service running under the AppHost, When the connection string is resolved,
    // Then the Aspire-injected ConnectionStrings:mongodb key is used.
    [Fact]
    public void Resolve_UsesAspireInjectedConnectionString_WhenPresent()
    {
        var configuration = Config(("ConnectionStrings:mongodb", "mongodb://aspire:27017"));

        Assert.Equal("mongodb://aspire:27017", MongoConnectionResolver.Resolve(configuration));
    }

    // Given a service started without the AppHost, When the connection string is resolved,
    // Then each legacy configuration shape is honoured in turn.
    [Theory]
    [InlineData("MongoDbSettings:ConnectionString")]
    [InlineData("MongoDB:ConnectionString")]
    [InlineData("LibrarySettings:MongoDB:ConnectionString")]
    public void Resolve_FallsBackToLegacyKey_WhenAspireKeyAbsent(string key)
    {
        var configuration = Config((key, "mongodb://legacy:27017"));

        Assert.Equal("mongodb://legacy:27017", MongoConnectionResolver.Resolve(configuration));
    }

    // Given more than one shape is populated, When resolving,
    // Then the earlier key in the documented order wins.
    [Fact]
    public void Resolve_PrefersEarlierKey_WhenSeveralArePresent()
    {
        var configuration = Config(
            ("ConnectionStrings:mongodb", "mongodb://first:27017"),
            ("MongoDbSettings:ConnectionString", "mongodb://second:27017"),
            ("MongoDB:ConnectionString", "mongodb://third:27017"),
            ("LibrarySettings:MongoDB:ConnectionString", "mongodb://fourth:27017"));

        Assert.Equal("mongodb://first:27017", MongoConnectionResolver.Resolve(configuration));
    }

    // A key present but blank is not a configured value — it must not shadow a later shape.
    [Fact]
    public void Resolve_TreatsWhitespaceAsAbsent()
    {
        var configuration = Config(
            ("ConnectionStrings:mongodb", "   "),
            ("MongoDB:ConnectionString", "mongodb://real:27017"));

        Assert.Equal("mongodb://real:27017", MongoConnectionResolver.Resolve(configuration));
    }

    // AC-2.5: Given no connection string is available anywhere, When a service starts,
    // Then it fails with a message naming every key tried — never a null-argument throw.
    [Fact]
    public void Resolve_ThrowsNamingEveryKeyTried_WhenNoneResolves()
    {
        var configuration = Config();

        var exception = Assert.Throws<InvalidOperationException>(
            () => MongoConnectionResolver.Resolve(configuration));

        Assert.Contains("ConnectionStrings:mongodb", exception.Message);
        Assert.Contains("MongoDbSettings:ConnectionString", exception.Message);
        Assert.Contains("MongoDB:ConnectionString", exception.Message);
        Assert.Contains("LibrarySettings:MongoDB:ConnectionString", exception.Message);
    }

    // The message has to tell the reader what to do, not merely what failed.
    [Fact]
    public void Resolve_FailureMessageIsActionable()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => MongoConnectionResolver.Resolve(Config()));

        Assert.Contains("AgendaBuddy.AppHost", exception.Message);
        Assert.Contains("ConnectionStrings__mongodb", exception.Message);
    }

    // Named settings follow the same discipline across every legacy prefix.
    [Theory]
    [InlineData("MongoDbSettings:DatabaseName")]
    [InlineData("MongoDB:DatabaseName")]
    [InlineData("LibrarySettings:MongoDB:DatabaseName")]
    public void ResolveSetting_ReadsNameFromEachPrefix(string key)
    {
        var configuration = Config((key, "configured_db"));

        Assert.Equal("configured_db",
            MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "agenda_buddy"));
    }

    // Standup rule 1: the name is per-call. Identity asks for CollectionName while the
    // domain services ask for per-entity names — one convention cannot serve both.
    [Fact]
    public void ResolveSetting_HonoursDifferentNamesPerCall()
    {
        var configuration = Config(
            ("MongoDbSettings:CollectionName", "users"),
            ("MongoDB:ProvidersCollection", "providers"),
            ("MongoDB:ProfessionsCollection", "professions"));

        Assert.Equal("users", MongoConnectionResolver.ResolveSetting(configuration, "CollectionName", "fallback"));
        Assert.Equal("providers", MongoConnectionResolver.ResolveSetting(configuration, "ProvidersCollection", "fallback"));
        Assert.Equal("professions", MongoConnectionResolver.ResolveSetting(configuration, "ProfessionsCollection", "fallback"));
    }

    // Standup rule 2: a missing collection name has a sane default, so this must not throw.
    [Fact]
    public void ResolveSetting_ReturnsDefault_WhenNoPrefixHasTheName()
    {
        Assert.Equal("agenda_buddy",
            MongoConnectionResolver.ResolveSetting(Config(), "DatabaseName", "agenda_buddy"));
    }

    [Fact]
    public void ResolveSetting_TreatsWhitespaceAsAbsent()
    {
        var configuration = Config(("MongoDB:DatabaseName", "  "));

        Assert.Equal("agenda_buddy",
            MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "agenda_buddy"));
    }

    [Fact]
    public void ResolveSetting_PrefersEarlierPrefix_WhenSeveralArePresent()
    {
        var configuration = Config(
            ("MongoDbSettings:DatabaseName", "first"),
            ("MongoDB:DatabaseName", "second"),
            ("LibrarySettings:MongoDB:DatabaseName", "third"));

        Assert.Equal("first",
            MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "fallback"));
    }
}
