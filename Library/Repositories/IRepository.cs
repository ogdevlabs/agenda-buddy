namespace Library.Repositories;

public interface IRepository<TEntity> where TEntity : class
{
    Task<IEnumerable<TEntity>> GetAllAsync();
    Task<TEntity> GetByIdAsync(string id);
    Task InsertAsync(TEntity entity);
    Task<bool> UpdateAsync(string id, TEntity entity);
    Task<bool> UpdateByIdentifierAsync(string identifier, TEntity entity);
    Task<bool> DeleteAsync(string id);
    Task<bool> DeleteByIdentifierAsync(string identifier);
    Task<TEntity> Find(BsonDocument filter);
    Task<IEnumerable<TEntity>> FindAllAsync(BsonDocument filter);
}