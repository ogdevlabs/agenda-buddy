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

    public async Task<IEnumerable<TEntity>> FindAllAsync(BsonDocument filter)
    {
        return await _collection.Find(filter).ToListAsync();
    }
}