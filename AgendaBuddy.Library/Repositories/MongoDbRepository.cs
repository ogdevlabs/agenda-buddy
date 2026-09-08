using AgendaBuddy.Library.Data;

namespace AgendaBuddy.Library.Repositories;

public class MongoDbRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
    private readonly IMongoCollection<TEntity> _collection;

    public MongoDbRepository(MongoClient dbClient, string databaseName, string collectionName)
    {
        var database = dbClient.GetDatabase(databaseName);
        _collection = database.GetCollection<TEntity>(collectionName);
    }

    public MongoDbRepository(IMongoDatabase database, string collectionName)
    {
        _collection = database.GetCollection<TEntity>(collectionName);
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        var documents = await _collection.Find(new BsonDocument()).ToListAsync();
        return documents;
    }

    public async Task<TEntity> GetByIdAsync(string id)
    {
        var objectId = new ObjectId(id);
        var filter = Builders<TEntity>.Filter.Eq("_id", objectId);
        var document = await _collection.Find(filter).FirstOrDefaultAsync();
        return document;
    }

    public async Task InsertAsync(TEntity entity)
    {
        await _collection.InsertOneAsync(entity);
    }

    /// <inheritdoc />
    /// <remarks>
    /// ⚠️ <b><c>MatchedCount</c>, not <c>ModifiedCount</c>, and the difference is a real defect this used to
    /// have.</b> MongoDB reports <c>ModifiedCount == 0</c> when the replacement is byte-identical to the stored
    /// document — nothing needed writing — which is indistinguishable from "no such document" under a
    /// <c>ModifiedCount &gt; 0</c> test. So <b>saving a form without changing a value reported failure</b>: the
    /// profile editor's own natural flow (open it, tick the two agreement boxes, Save) leaves the name and phone
    /// untouched, so the whole-document replace was a no-op, the route answered <c>404</c>, and the user was told
    /// "could not save your profile — try again" on a request that had in fact succeeded.
    /// <para>
    /// <c>MatchedCount</c> answers the question the callers are actually asking: does the document exist, and does
    /// it now hold what I sent? Both are true for a no-op.
    /// </para>
    /// </remarks>
    public async Task<bool> UpdateAsync(string id, TEntity entity)
    {
        var objectId = new ObjectId(id);
        var filter = Builders<TEntity>.Filter.Eq("_id", objectId);
        var result = await _collection.ReplaceOneAsync(filter, entity);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    /// <inheritdoc />
    /// <remarks>See <see cref="UpdateAsync"/> for why this counts matches rather than modifications.</remarks>
    public async Task<bool> UpdateByIdentifierAsync(string identifier, TEntity entity)
    {
        var filter = Builders<TEntity>.Filter.Eq("identifier", identifier);
        var result = await _collection.ReplaceOneAsync(filter, entity);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var objectId = new ObjectId(id);
        var filter = Builders<TEntity>.Filter.Eq("_id", objectId);
        var result = await _collection.DeleteOneAsync(filter);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    public async Task<bool> DeleteByIdentifierAsync(string identifier)
    {
        var filter = Builders<TEntity>.Filter.Eq("identifier", identifier);
        var result = await _collection.DeleteOneAsync(filter);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    public async Task<TEntity> Find(BsonDocument filter)
    {
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<TEntity?> FindOneAsync(BsonDocument filter)
    {
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<TEntity?> FindOneAndDeleteAsync(BsonDocument filter)
    {
        return await _collection.FindOneAndDeleteAsync(filter);
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(BsonDocument filter)
    {
        return await _collection.Find(filter).ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> FindAllAsync(BsonDocument filter, BsonDocument sort, int limit)
    {
        return await _collection.Find(filter).Sort(sort).Limit(Math.Max(1, limit)).ToListAsync();
    }

    /// <inheritdoc />
    public async Task<long> UpdateManyAsync(BsonDocument filter, BsonDocument update)
    {
        var result = await _collection.UpdateManyAsync(filter, update);
        return result.IsAcknowledged ? result.ModifiedCount : 0;
    }

    public async Task<long> DeleteManyAsync(BsonDocument filter)
    {
        var result = await _collection.DeleteManyAsync(filter);
        return result.IsAcknowledged ? result.DeletedCount : 0;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>IsUpsert</c> is left at its default of <c>false</c> and no option sets it — AC-9 depends
    /// on that, and it is stated here because the next person to add an overload will be tempted.
    /// <c>ReturnDocument.After</c> is what makes the returned counter usable for the lockout decision.
    /// </remarks>
    public async Task<TEntity?> FindOneAndUpdateAsync(BsonDocument filter, BsonDocument update)
    {
        var options = new FindOneAndUpdateOptions<TEntity>
        {
            ReturnDocument = ReturnDocument.After,
            IsUpsert = false
        };

        return await _collection.FindOneAndUpdateAsync<TEntity>(filter, update, options);
    }

    public Task<(IEnumerable<TEntity> Items, long TotalCount)> GetPagedAsync(int skip, int take) =>
        GetPagedAsync(new BsonDocument(), skip, take);

    public async Task<(IEnumerable<TEntity> Items, long TotalCount)> GetPagedAsync(
        BsonDocument filter, int skip, int take)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // Both the count and the page use the SAME filter, so TotalCount describes the set the caller can
        // actually reach. Counting unfiltered here would make the last page look non-empty when it is not.
        var totalCount = await _collection.CountDocumentsAsync(filter);

        // Normalised because Skip(-1) throws on the driver but is a silent no-op in LINQ, and
        // InMemoryCredentialRepository implements the same interface. Divergent behaviour between two
        // implementers of one contract is the kind of defect that only ever appears in production.
        var items = await _collection
            .Find(filter)
            .Skip(Math.Max(0, skip))
            .Limit(Math.Max(0, take))
            .ToListAsync();

        return (items, totalCount);
    }
}
