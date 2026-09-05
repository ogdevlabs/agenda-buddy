using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using MongoDB.Bson;

namespace AgendaBuddy.Identity.Tests.Helpers;

/// <summary>
/// In-memory IRepository<CredentialEntity> for IdentityService unit tests.
/// Supports FindOneAsync by simple field predicate and FindOneAndDeleteAsync.
/// </summary>
public class InMemoryCredentialRepository : IRepository<CredentialEntity>
{
    private readonly List<CredentialEntity> _store = new();

    public Task<IEnumerable<CredentialEntity>> GetAllAsync() =>
        Task.FromResult<IEnumerable<CredentialEntity>>(_store.ToList());

    public Task<CredentialEntity> GetByIdAsync(string id) =>
        Task.FromResult(_store.FirstOrDefault(e => e.Id == id)!);

    public Task InsertAsync(CredentialEntity entity)
    {
        _store.Add(entity);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(string id, CredentialEntity entity)
    {
        var idx = _store.FindIndex(e => e.Id == id);
        if (idx < 0) return Task.FromResult(false);
        _store[idx] = entity;
        // Counted, not forbidden: this is a whole-document replacement (MongoDbRepository issues
        // ReplaceOneAsync), and AC-11 asserts that no credential write takes this path.
        WholeDocumentReplacements++;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateByIdentifierAsync(string identifier, CredentialEntity entity) =>
        Task.FromResult(false);

    public Task<bool> DeleteAsync(string id)
    {
        var item = _store.FirstOrDefault(e => e.Id == id);
        if (item is null) return Task.FromResult(false);
        _store.Remove(item);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteByIdentifierAsync(string identifier) =>
        Task.FromResult(false);

    public Task<CredentialEntity> Find(BsonDocument filter) =>
        Task.FromResult(FindByFilter(filter)!);

    public Task<CredentialEntity?> FindOneAsync(BsonDocument filter) =>
        Task.FromResult(FindByFilter(filter));

    public Task<CredentialEntity?> FindOneAndDeleteAsync(BsonDocument filter)
    {
        var item = FindByFilterWithExpiry(filter);
        if (item is not null) _store.Remove(item);
        return Task.FromResult(item);
    }

    public Task<IEnumerable<CredentialEntity>> FindAllAsync(BsonDocument filter) =>
        Task.FromResult<IEnumerable<CredentialEntity>>(_store.Where(e =>
            MatchesFilter(e, filter)).ToList());

    // Unimplemented rather than approximated, matching this double's stance everywhere else: no credential
    // path sorts or bulk-updates, so a plausible-looking stub would only be able to mislead. Implement it
    // here the day one does.
    public Task<IEnumerable<CredentialEntity>> FindAllAsync(BsonDocument filter, BsonDocument sort, int limit) =>
        throw new NotSupportedException(
            "InMemoryCredentialRepository does not implement sorted, bounded reads. No credential path uses " +
            "them; implement it here rather than letting a test pass on an order it never applied.");

    public Task<long> UpdateManyAsync(BsonDocument filter, BsonDocument update) =>
        throw new NotSupportedException(
            "InMemoryCredentialRepository does not implement multi-document updates. No credential path uses " +
            "them; implement it here rather than letting a test pass on a write it never applied.");

    // ADR-023's repository half. Negatives are normalised to zero to match
    // MongoDbRepository, where Skip(-1) throws rather than being the no-op LINQ makes it.
    public Task<(IEnumerable<CredentialEntity> Items, long TotalCount)> GetPagedAsync(int skip, int take) =>
        GetPagedAsync(new BsonDocument(), skip, take);

    /// <summary>
    /// Filtered paging, over the same <see cref="MatchesFilter"/> the other reads use — so this double
    /// applies the caller's filter for real rather than ignoring it, which would let a test pass on a
    /// filter that was never evaluated.
    /// </summary>
    public Task<(IEnumerable<CredentialEntity> Items, long TotalCount)> GetPagedAsync(
        BsonDocument filter, int skip, int take)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var matched = _store.Where(entity => MatchesFilter(entity, filter)).ToList();

        // Counted from the matched set, matching MongoDbRepository: TotalCount describes what the caller
        // can reach, not the whole collection.
        return Task.FromResult<(IEnumerable<CredentialEntity>, long)>((
            matched.Skip(Math.Max(0, skip)).Take(Math.Max(0, take)).ToList(),
            matched.Count));
    }

    /// <summary>
    /// Runs between matching a document and applying the update, so a test can inject the fault that
    /// used to destroy an account.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AC-2. Before this existed, "a fault between the read and the write of a
    /// rotation" was <b>unexpressible</b> as a test (<c>11-testing.md:65</c>) — which is precisely how a
    /// delete-then-insert survived in <c>RefreshAsync</c> with 20 passing tests around it. The hook fires
    /// after the filter has matched and before any mutation, which is the window the old code left open.
    /// </para>
    /// <para>
    /// Throw a <see cref="MongoDB.Driver.MongoException"/> from it to reproduce the case the PRD calls
    /// out: a transient database fault on the <b>handled</b> path, where the caller returns a tidy 503 to
    /// a user whose account no longer exists.
    /// </para>
    /// </remarks>
    public Action? FaultBetweenMatchAndWrite { get; set; }

    /// <summary>
    /// Every update document this repository has applied, in order — so a test can assert on the
    /// <i>shape</i> of a write and not merely its effect (AC-11).
    /// </summary>
    public List<BsonDocument> AppliedUpdates { get; } = [];

    /// <summary>
    /// How many times a whole document was replaced. Credential writes must never replace a
    /// document, so the assertion is that this stays at zero.
    /// </summary>
    public int WholeDocumentReplacements { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// A deliberately narrow evaluator: it supports exactly the operators this repository's callers use and
    /// <b>throws on anything else</b>. A test double that silently ignored an unsupported operator
    /// would report green for a filter MongoDB would have evaluated differently, which is worse than
    /// having no double at all.
    /// </remarks>
    public Task<CredentialEntity?> FindOneAndUpdateAsync(BsonDocument filter, BsonDocument update)
    {
        var match = _store.FirstOrDefault(e => MatchesStrictFilter(e, filter));
        if (match is null) return Task.FromResult<CredentialEntity?>(null);

        FaultBetweenMatchAndWrite?.Invoke();

        Apply(match, update);
        AppliedUpdates.Add(update);

        // A COPY, because MongoDB returns a deserialized document and not a handle on the stored one.
        // Returning the live instance would let a caller mutate the store by accident, and would make
        // two successive post-images compare equal — which is the opposite of what the post-image
        // guarantee is for.
        return Task.FromResult<CredentialEntity?>(Snapshot(match));
    }

    private static CredentialEntity Snapshot(CredentialEntity entity) => new()
    {
        Id = entity.Id,
        Email = entity.Email,
        PasswordHash = entity.PasswordHash,
        Role = entity.Role,
        MustResetPassword = entity.MustResetPassword,
        FailedAttempts = entity.FailedAttempts,
        LockUntil = entity.LockUntil,
        RefreshToken = entity.RefreshToken is null
            ? null
            : new RefreshTokenDocument
            {
                Hash = entity.RefreshToken.Hash,
                Expiry = entity.RefreshToken.Expiry
            },
        ResetToken = entity.ResetToken is null
            ? null
            : new PasswordResetTokenDocument
            {
                Hash = entity.ResetToken.Hash,
                Expiry = entity.ResetToken.Expiry
            }
    };

    private static void Apply(CredentialEntity entity, BsonDocument update)
    {
        foreach (var op in update)
        {
            var operand = op.Value.AsBsonDocument;
            switch (op.Name)
            {
                case "$set":
                    foreach (var field in operand) Set(entity, field.Name, field.Value);
                    break;
                case "$unset":
                    foreach (var field in operand) Unset(entity, field.Name);
                    break;
                case "$inc":
                    foreach (var field in operand) Increment(entity, field.Name, field.Value.ToInt32());
                    break;
                default:
                    throw new NotSupportedException(
                        $"InMemoryCredentialRepository does not implement the update operator '{op.Name}'. " +
                        "Implement it here rather than letting a test pass on a write MongoDB would " +
                        "have applied differently.");
            }
        }
    }

    private static void Set(CredentialEntity entity, string field, BsonValue value)
    {
        switch (field)
        {
            case "refresh_token":
                entity.RefreshToken = value.IsBsonNull
                    ? null
                    : new RefreshTokenDocument
                    {
                        Hash = value.AsBsonDocument["hash"].AsString,
                        // MongoDB stores milliseconds, so a round trip through BSON truncates. Faithful
                        // to the real driver on purpose: a test asserting sub-millisecond equality
                        // should fail here too, not only against a container.
                        Expiry = value.AsBsonDocument["expiry"].ToUniversalTime()
                    };
                break;
            case "reset_token":
                entity.ResetToken = value.IsBsonNull
                    ? null
                    : new PasswordResetTokenDocument
                    {
                        Hash = value.AsBsonDocument["hash"].AsString,
                        Expiry = value.AsBsonDocument["expiry"].ToUniversalTime()
                    };
                break;
            case "email_verification_token":
                entity.EmailVerificationToken = value.IsBsonNull
                    ? null
                    : new EmailVerificationTokenDocument
                    {
                        Hash = value.AsBsonDocument["hash"].AsString,
                        Expiry = value.AsBsonDocument["expiry"].ToUniversalTime()
                    };
                break;
            case "email_verified":
                entity.EmailVerified = value.AsBoolean;
                break;
            case "failed_attempts":
                entity.FailedAttempts = value.ToInt32();
                break;
            case "lock_until":
                entity.LockUntil = value.IsBsonNull ? null : value.ToUniversalTime();
                break;
            case "password_hash":
                entity.PasswordHash = value.AsString;
                break;
            case "must_reset_password":
                entity.MustResetPassword = value.AsBoolean;
                break;
            default:
                throw new NotSupportedException($"$set on unmapped field '{field}'.");
        }
    }

    private static void Unset(CredentialEntity entity, string field)
    {
        switch (field)
        {
            case "lock_until": entity.LockUntil = null; break;
            case "refresh_token": entity.RefreshToken = null; break;
            case "reset_token": entity.ResetToken = null; break;
            case "email_verification_token": entity.EmailVerificationToken = null; break;
            default: throw new NotSupportedException($"$unset on unmapped field '{field}'.");
        }
    }

    private static void Increment(CredentialEntity entity, string field, int by)
    {
        if (field != "failed_attempts")
            throw new NotSupportedException($"$inc on unmapped field '{field}'.");

        // $inc on a missing field creates it at the increment value — the C# default of 0 gives the
        // same answer, which is why no migration is needed (data-model.md §7).
        entity.FailedAttempts += by;
    }

    /// <summary>
    /// Filter evaluation that refuses what it does not understand, unlike
    /// <see cref="MatchesFilter"/>, which ignores unknown fields.
    /// </summary>
    private static bool MatchesStrictFilter(CredentialEntity e, BsonDocument filter)
    {
        foreach (var clause in filter)
        {
            if (!MatchesClause(e, clause.Name, clause.Value)) return false;
        }

        return true;
    }

    private static bool MatchesClause(CredentialEntity e, string name, BsonValue condition)
    {
        if (name == "$or")
        {
            return condition.AsBsonArray.Any(
                alternative => MatchesStrictFilter(e, alternative.AsBsonDocument));
        }

        return name switch
        {
            "email" => Compare(e.Email, condition),
            "role" => Compare(e.Role, condition),
            "refresh_token.hash" => Compare(e.RefreshToken?.Hash, condition),
            "refresh_token.expiry" => Compare(e.RefreshToken?.Expiry, condition),
            "reset_token.hash" => Compare(e.ResetToken?.Hash, condition),
            "reset_token.expiry" => Compare(e.ResetToken?.Expiry, condition),
            "email_verification_token.hash" => Compare(e.EmailVerificationToken?.Hash, condition),
            "email_verification_token.expiry" => Compare(e.EmailVerificationToken?.Expiry, condition),
            "failed_attempts" => Compare(e.FailedAttempts, condition),
            "lock_until" => Compare(e.LockUntil, condition),
            _ => throw new NotSupportedException(
                $"InMemoryCredentialRepository cannot evaluate the filter field '{name}'. Add it here " +
                "rather than letting a test pass on a filter it never really applied.")
        };
    }

    private static bool Compare(string? actual, BsonValue condition) =>
        condition.IsBsonNull ? actual is null : actual == condition.AsString;

    private static bool Compare(int actual, BsonValue condition)
    {
        if (!condition.IsBsonDocument) return actual == condition.ToInt32();

        foreach (var op in condition.AsBsonDocument)
        {
            var expected = op.Value.ToInt32();
            var satisfied = op.Name switch
            {
                "$gte" => actual >= expected,
                "$gt" => actual > expected,
                "$lte" => actual <= expected,
                "$lt" => actual < expected,
                _ => throw new NotSupportedException($"Unsupported int operator '{op.Name}'.")
            };
            if (!satisfied) return false;
        }

        return true;
    }

    private static bool Compare(DateTime? actual, BsonValue condition)
    {
        if (condition.IsBsonNull) return actual is null;

        if (!condition.IsBsonDocument)
            return actual == condition.ToUniversalTime();

        foreach (var op in condition.AsBsonDocument)
        {
            var expected = op.Value.ToUniversalTime();

            // A missing field satisfies no comparison operator in MongoDB, which is what makes the
            // "lock_until is null OR in the past" filter need both branches of its $or.
            if (actual is null) return false;

            var satisfied = op.Name switch
            {
                "$gt" => actual > expected,
                "$gte" => actual >= expected,
                "$lt" => actual < expected,
                "$lte" => actual <= expected,
                _ => throw new NotSupportedException($"Unsupported date operator '{op.Name}'.")
            };
            if (!satisfied) return false;
        }

        return true;
    }

    // Simple filter evaluation for test purposes
    private CredentialEntity? FindByFilter(BsonDocument filter)
    {
        return _store.FirstOrDefault(e => MatchesFilter(e, filter));
    }

    private CredentialEntity? FindByFilterWithExpiry(BsonDocument filter)
    {
        // Supports: {"refresh_token.hash": hash, "refresh_token.expiry": {$gt: utcNow}}
        string? hashFilter = null;
        DateTime? expiryGt = null;

        foreach (var elem in filter)
        {
            if (elem.Name == "refresh_token.hash")
                hashFilter = elem.Value.AsString;
            else if (elem.Name == "refresh_token.expiry" && elem.Value.IsBsonDocument)
            {
                var doc = elem.Value.AsBsonDocument;
                if (doc.Contains("$gt"))
                    expiryGt = doc["$gt"].ToUniversalTime();
            }
        }

        return _store.FirstOrDefault(e =>
            (hashFilter is null || e.RefreshToken?.Hash == hashFilter) &&
            (expiryGt is null || (e.RefreshToken?.Expiry > expiryGt)));
    }

    private static bool MatchesFilter(CredentialEntity e, BsonDocument filter)
    {
        foreach (var elem in filter)
        {
            switch (elem.Name)
            {
                case "email" when e.Email != elem.Value.AsString: return false;
                case "refresh_token.hash" when e.RefreshToken?.Hash != elem.Value.AsString: return false;
            }
        }
        return true;
    }
}
