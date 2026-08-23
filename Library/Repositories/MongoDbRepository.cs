using Library.Data;

namespace Library.Repositories;

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

    public async Task<bool> UpdateAsync(string id, TEntity entity)
    {
        var objectId = new ObjectId(id);
        var filter = Builders<TEntity>.Filter.Eq("_id", objectId);
        var result = await _collection.ReplaceOneAsync(filter, entity);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task<bool> UpdateByIdentifierAsync(string identifier, TEntity entity)
    {
        var filter = Builders<TEntity>.Filter.Eq("identifier", identifier);
        var result = await _collection.ReplaceOneAsync(filter, entity);
        return result.IsAcknowledged && result.ModifiedCount > 0;
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
    /// <remarks>
    /// <c>IsUpsert</c> is left at its default of <c>false</c> and no option sets it — F-021 AC-9 depends
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

    public async Task<(IEnumerable<TEntity> Items, long TotalCount)> GetPagedAsync(int skip, int take)
    {
        // Normalised because Skip(-1) throws on the driver but is a silent no-op in LINQ, and
        // InMemoryCredentialRepository implements the same interface. Divergent behaviour between two
        // implementers of one contract is the kind of defect that only ever appears in production.
        var filter = new BsonDocument();

        var totalCount = await _collection.CountDocumentsAsync(filter);
        var items = await _collection
            .Find(filter)
            .Skip(Math.Max(0, skip))
            .Limit(Math.Max(0, take))
            .ToListAsync();

        return (items, totalCount);
    }
}