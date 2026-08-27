using System;
using System.Threading.Tasks;
using AgendaBuddy.Library.Entities;
using MongoDB.Bson;
using Xunit;

namespace AgendaBuddy.Identity.Tests.Helpers;

/// <summary>
/// The semantics of <c>FindOneAndUpdateAsync</c>, pinned against the in-memory implementer.
/// </summary>
/// <remarks>
/// <para>
/// The same split as <c>GetPagedAsync</c> follows here: the <b>contract</b> is
/// pinned in <c>Library.Tests</c> (the shape callers compile against), the <b>semantics</b>
/// here, and <b>MongoDB's own behaviour</b> by <c>CredentialUpdatePrimitiveTest</c> on the integration
/// harness — because <c>MongoDbRepository&lt;T&gt;</c> takes an <c>IMongoDatabase</c> and cannot be unit
/// tested.
/// </para>
/// <para>
/// The double is deliberately strict: an operator or field it does not implement throws rather than
/// being ignored. A double that quietly skipped a filter clause would report green for a query MongoDB
/// would have answered differently, which is the failure mode that makes test doubles worse than no
/// coverage.
/// </para>
/// </remarks>
public class InMemoryCredentialRepositoryUpdateTest
{
    private static InMemoryCredentialRepository WithOneCredential(out CredentialEntity credential)
    {
        var repo = new InMemoryCredentialRepository();
        credential = new CredentialEntity
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Email = "seed@example.com",
            PasswordHash = "hash",
            Role = "Provider"
        };
        repo.InsertAsync(credential).GetAwaiter().GetResult();
        return repo;
    }

    [Fact]
    public async Task AFilterThatMatchesNothing_WritesNothingAndCreatesNothing()
    {
        // AC-5. Never upserting is what stops a failed login for an unknown address from
        // materialising a credential document.
        var repo = WithOneCredential(out _);

        var result = await repo.FindOneAndUpdateAsync(
            new BsonDocument("email", "absent@example.com"),
            new BsonDocument("$inc", new BsonDocument("failed_attempts", 1)));

        Assert.Null(result);
        Assert.Single(await repo.GetAllAsync());
    }

    [Fact]
    public async Task ItReturnsThePostImage_SoTheNewCounterIsUsableWithoutASecondRead()
    {
        var repo = WithOneCredential(out _);

        var first = await repo.FindOneAndUpdateAsync(
            new BsonDocument("email", "seed@example.com"),
            new BsonDocument("$inc", new BsonDocument("failed_attempts", 1)));
        var second = await repo.FindOneAndUpdateAsync(
            new BsonDocument("email", "seed@example.com"),
            new BsonDocument("$inc", new BsonDocument("failed_attempts", 1)));

        Assert.Equal(1, first!.FailedAttempts);
        Assert.Equal(2, second!.FailedAttempts);
    }

    [Fact]
    public async Task UnsetRemovesTheLock_AndUnsettingAnAbsentFieldIsANoOp()
    {
        var repo = WithOneCredential(out var credential);
        credential.LockUntil = DateTime.UtcNow.AddMinutes(5);

        var update = new BsonDocument("$unset", new BsonDocument("lock_until", ""));

        Assert.Null((await repo.FindOneAndUpdateAsync(new BsonDocument("email", credential.Email), update))!.LockUntil);
        Assert.Null((await repo.FindOneAndUpdateAsync(new BsonDocument("email", credential.Email), update))!.LockUntil);
    }

    [Fact]
    public async Task AnOrFilter_MatchesAnAbsentFieldAndAPastInstantButNotAFutureOne()
    {
        // The exact filter the rotation path uses for "not locked". Both branches are needed: in
        // MongoDB a missing field satisfies no comparison operator, so `lock_until <= now` alone would
        // never match an account that has never been locked.
        var repo = WithOneCredential(out var credential);
        var now = new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

        var notLocked = new BsonDocument
        {
            { "email", credential.Email },
            {
                "$or", new BsonArray
                {
                    new BsonDocument("lock_until", BsonNull.Value),
                    new BsonDocument("lock_until", new BsonDocument("$lte", now))
                }
            }
        };
        var touch = new BsonDocument("$inc", new BsonDocument("failed_attempts", 1));

        Assert.NotNull(await repo.FindOneAndUpdateAsync(notLocked, touch));

        credential.LockUntil = now.AddMinutes(-1);
        Assert.NotNull(await repo.FindOneAndUpdateAsync(notLocked, touch));

        credential.LockUntil = now.AddMinutes(1);
        Assert.Null(await repo.FindOneAndUpdateAsync(notLocked, touch));
    }

    [Fact]
    public async Task TheFaultHookRunsBetweenMatchingAndWriting()
    {
        // PRD requirement 4. Before this hook, "a fault between the read and the write" was not
        // expressible as a test (11-testing.md:65) — which is how a delete-then-insert survived.
        var repo = WithOneCredential(out var credential);
        repo.FaultBetweenMatchAndWrite = () => throw new InvalidOperationException("boom");

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.FindOneAndUpdateAsync(
            new BsonDocument("email", credential.Email),
            new BsonDocument("$inc", new BsonDocument("failed_attempts", 1))));

        Assert.Equal(0, credential.FailedAttempts);
    }

    [Fact]
    public async Task AnUnsupportedOperator_Throws_RatherThanPassingVacuously()
    {
        var repo = WithOneCredential(out var credential);

        await Assert.ThrowsAsync<NotSupportedException>(() => repo.FindOneAndUpdateAsync(
            new BsonDocument("email", credential.Email),
            new BsonDocument("$push", new BsonDocument("failed_attempts", 1))));

        await Assert.ThrowsAsync<NotSupportedException>(() => repo.FindOneAndUpdateAsync(
            new BsonDocument("role_name_that_does_not_exist", "x"),
            new BsonDocument("$inc", new BsonDocument("failed_attempts", 1))));
    }
}
