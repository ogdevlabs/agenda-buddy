using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// <c>MongoDbRepository&lt;T&gt;.FindOneAndUpdateAsync</c> against a real MongoDB — the semantics F-021's
/// whole design rests on, verified by the engine rather than by the in-memory double that encodes my
/// beliefs about it.
/// </summary>
/// <remarks>
/// <para>
/// F-016 recorded as debt that <c>MongoDbRepository&lt;T&gt;.GetPagedAsync</c> had <b>no</b> test of its
/// Mongo semantics, because the class takes an <c>IMongoDatabase</c> and the driver's fluent chain ends in
/// an extension method Moq cannot intercept. The same is true of this primitive, and the same answer
/// applies: run it against the container.
/// </para>
/// <para>
/// Three of these assertions are load-bearing for correctness claims made elsewhere:
/// </para>
/// <list type="bullet">
/// <item><b>No upsert</b> — AC-9's "a failed login for an unknown email creates nothing" is a property of
/// this method, not of its callers, and only MongoDB can confirm it.</item>
/// <item><b>A <c>$set</c> on <c>refresh_token</c> leaves siblings untouched</b> — AC-1. In BSON that is
/// obvious; it is also the exact thing the deleted-and-reinserted document got wrong.</item>
/// <item><b>The "not locked" <c>$or</c> matches a document with no <c>lock_until</c> field at all</b> — in
/// MongoDB a missing field satisfies no comparison operator, so a filter of <c>lock_until &lt;= now</c>
/// alone would silently never match an account that has never been locked, which is nearly all of
/// them.</item>
/// </list>
/// </remarks>
[Collection(HarnessCollection.Name)]
public class CredentialUpdatePrimitiveTest(ServiceHostFixture<ProfessionAnchor> host)
    : IClassFixture<ServiceHostFixture<ProfessionAnchor>>
{
    private const string Email = "primitive@example.com";
    private const string Collection = "credentials";

    private static readonly DateTime Now = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// A repository over this test's own database. The hosted service is incidental — the fixture is used
    /// for its container and its per-test database, not for its routes.
    /// </summary>
    private static async Task<(IRepository<CredentialEntity> Repository, IMongoCollection<CredentialEntity> Raw)>
        SeedAsync(ServiceHost service, Action<CredentialEntity>? customise = null)
    {
        var raw = service.Database.GetCollection<CredentialEntity>(Collection);

        var credential = new CredentialEntity
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Email = Email,
            PasswordHash = "$2a$12$notarealhashbutthelengthisplausible00000000000000000000",
            Role = "Provider",
            MustResetPassword = true,
            RefreshToken = new RefreshTokenDocument { Hash = "old-hash", Expiry = Now.AddHours(24) }
        };

        customise?.Invoke(credential);
        await raw.InsertOneAsync(credential);

        return (new MongoDbRepository<CredentialEntity>(service.Database, Collection), raw);
    }

    [Fact]
    public async Task AFilterMatchingNothing_ReturnsNullAndCreatesNoDocument()
    {
        // AC-5 / AC-9 against the real driver, which is the only place "IsUpsert is left false" means
        // anything.
        using var service = host.StartService();
        var (repository, raw) = await SeedAsync(service);

        var result = await repository.FindOneAndUpdateAsync(
            new BsonDocument("email", "nobody@example.com"),
            new BsonDocument("$inc", new BsonDocument("failed_attempts", 1)));

        Assert.Null(result);
        Assert.Equal(1, await raw.CountDocumentsAsync(new BsonDocument()));
    }

    [Fact]
    public async Task IncrementingAnAbsentField_CreatesItAtOne()
    {
        // data-model.md §7's migration claim: existing credential documents have no failed_attempts, and
        // $inc on a missing field creates it at the increment value. That is what makes F-021 need no
        // migration script, so it is asserted rather than cited.
        using var service = host.StartService();
        var (repository, _) = await SeedAsync(service);

        var first = await repository.FindOneAndUpdateAsync(
            new BsonDocument("email", Email),
            new BsonDocument("$inc", new BsonDocument("failed_attempts", 1)));

        Assert.Equal(1, first!.FailedAttempts);
    }

    [Fact]
    public async Task ItReturnsThePostImage_NotThePreImage()
    {
        // The lockout decision reads the returned counter instead of issuing a second query, so a
        // pre-image would silently make the threshold fire one attempt late.
        using var service = host.StartService();
        var (repository, _) = await SeedAsync(service);

        var update = new BsonDocument("$inc", new BsonDocument("failed_attempts", 1));

        Assert.Equal(1, (await repository.FindOneAndUpdateAsync(new BsonDocument("email", Email), update))!.FailedAttempts);
        Assert.Equal(2, (await repository.FindOneAndUpdateAsync(new BsonDocument("email", Email), update))!.FailedAttempts);
    }

    [Fact]
    public async Task SettingTheRefreshSubdocument_LeavesEverySiblingFieldIntact()
    {
        // AC-1 at the storage layer. The defect this feature exists to fix was a whole-document delete
        // and re-insert; this is the assertion that the replacement really is surgical.
        using var service = host.StartService();
        var (repository, raw) = await SeedAsync(service);

        var updated = await repository.FindOneAndUpdateAsync(
            new BsonDocument("refresh_token.hash", "old-hash"),
            new BsonDocument(
                "$set",
                new BsonDocument(
                    "refresh_token",
                    new BsonDocument { { "hash", "new-hash" }, { "expiry", Now.AddHours(48) } })));

        Assert.NotNull(updated);
        Assert.Equal("new-hash", updated.RefreshToken!.Hash);
        Assert.Equal(Email, updated.Email);
        Assert.Equal("Provider", updated.Role);
        Assert.True(updated.MustResetPassword);
        Assert.StartsWith("$2a$12$", updated.PasswordHash);
        Assert.Equal(1, await raw.CountDocumentsAsync(new BsonDocument()));
    }

    [Fact]
    public async Task AReplayedTokenHash_MatchesNothingOnceRotated()
    {
        // AC-3's mechanism, at the layer that enforces it: single use comes from the old hash being part
        // of the filter, so the second presentation matches no document. No delete required.
        using var service = host.StartService();
        var (repository, _) = await SeedAsync(service);

        var rotate = new BsonDocument(
            "$set",
            new BsonDocument(
                "refresh_token",
                new BsonDocument { { "hash", "new-hash" }, { "expiry", Now.AddHours(48) } }));

        Assert.NotNull(await repository.FindOneAndUpdateAsync(
            new BsonDocument("refresh_token.hash", "old-hash"), rotate));
        Assert.Null(await repository.FindOneAndUpdateAsync(
            new BsonDocument("refresh_token.hash", "old-hash"), rotate));
    }

    [Theory]
    [InlineData(null, true)]      // never locked — the field is absent entirely
    [InlineData(-1, true)]        // lock expired an hour ago
    [InlineData(1, false)]        // locked for another hour
    public async Task TheNotLockedFilter_TreatsAnAbsentOrPastLockAsUnlocked(
        int? lockOffsetHours, bool shouldMatch)
    {
        using var service = host.StartService();
        var (repository, _) = await SeedAsync(service, credential =>
            credential.LockUntil = lockOffsetHours is null ? null : Now.AddHours(lockOffsetHours.Value));

        var filter = new BsonDocument
        {
            { "email", Email },
            {
                "$or", new BsonArray
                {
                    new BsonDocument("lock_until", BsonNull.Value),
                    new BsonDocument("lock_until", new BsonDocument("$lte", Now))
                }
            }
        };

        var result = await repository.FindOneAndUpdateAsync(
            filter, new BsonDocument("$inc", new BsonDocument("failed_attempts", 1)));

        Assert.Equal(shouldMatch, result is not null);
    }

    [Fact]
    public async Task UnsettingAnAbsentField_IsANoOp()
    {
        // The success path unsets lock_until on every login, including for accounts that have never been
        // locked — which is almost all of them, almost always.
        using var service = host.StartService();
        var (repository, _) = await SeedAsync(service);

        var result = await repository.FindOneAndUpdateAsync(
            new BsonDocument("email", Email),
            new BsonDocument("$unset", new BsonDocument("lock_until", "")));

        Assert.NotNull(result);
        Assert.Null(result.LockUntil);
    }
}
