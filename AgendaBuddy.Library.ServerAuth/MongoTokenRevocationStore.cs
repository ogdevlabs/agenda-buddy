using AgendaBuddy.Library.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AgendaBuddy.Library.Security;

/// <summary>
/// Cross-service revocation denylist backed by a shared MongoDB collection rather than the
/// per-process <c>IDistributedCache</c> — the latter cannot see a revocation written by another
/// of the seven services. Documents are keyed by <c>jti</c> and self-expire via a TTL index, so
/// a revoked entry never outlives the token it revokes.
/// </summary>
public class MongoTokenRevocationStore : ITokenRevocationStore
{
    private const string CollectionName = "revoked_tokens";
    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoTokenRevocationStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<BsonDocument>(CollectionName);
    }

    /// <summary>
    /// Idempotent — safe to call at every service startup. <c>expireAfterSeconds: 0</c> makes
    /// MongoDB's background reaper delete a document at the exact instant its own
    /// <c>expires_at</c> passes, so the denylist never grows past the tokens that are still
    /// actually valid.
    /// </summary>
    public async Task EnsureIndexAsync()
    {
        var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending("expires_at");
        var indexOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.Zero };
        await _collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(indexKeys, indexOptions));
    }

    public async Task RevokeAsync(string jti, DateTimeOffset expiresAtUtc)
    {
        var document = new BsonDocument
        {
            { "_id", jti },
            { "expires_at", expiresAtUtc.UtcDateTime },
        };

        // A token can only be revoked once anyway (its own jti is unique per mint), but replace
        // rather than insert so a duplicate call — e.g. a retried logout — never 11000-conflicts.
        await _collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", jti),
            document,
            new ReplaceOptions { IsUpsert = true });
    }

    public async Task<bool> IsRevokedAsync(string jti)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", jti);
        var match = await _collection.Find(filter).FirstOrDefaultAsync();
        return match is not null;
    }
}
