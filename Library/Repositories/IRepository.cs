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
    Task<TEntity?> FindOneAsync(BsonDocument filter);
    Task<TEntity?> FindOneAndDeleteAsync(BsonDocument filter);
    Task<IEnumerable<TEntity>> FindAllAsync(BsonDocument filter);

    /// <summary>
    /// One page of the collection, plus the total number of documents in it.
    /// </summary>
    /// <param name="skip">Documents to skip. Negative values are treated as zero.</param>
    /// <param name="take">Maximum documents to return. Negative values are treated as zero.</param>
    /// <returns>
    /// The page, and the count of <b>all</b> documents in the collection — not the size of the page.
    /// A <paramref name="skip"/> past the end returns an empty page with the full count.
    /// </returns>
    /// <remarks>
    /// ADR-023's repository half (F-016-T10). One primitive rather than a query abstraction: the
    /// requirement is two paginated list endpoints, not a query DSL. <c>TotalCount</c> is
    /// <see cref="long"/> because <c>CountDocumentsAsync</c> returns <see cref="long"/>, and
    /// <c>api-contracts.md</c> §4 publishes <c>totalCount</c> as <see cref="long"/> to F-015 for the
    /// same reason.
    /// <para>
    /// Capping and clamping the caller's page size is the <b>endpoint's</b> job (ADR-023: clamp,
    /// never reject), not this method's. The page cap is a security control — an uncapped page size
    /// restores the full-dataset dump F-016 exists to remove — so it belongs where the untrusted
    /// value arrives.
    /// </para>
    /// </remarks>
    Task<(IEnumerable<TEntity> Items, long TotalCount)> GetPagedAsync(int skip, int take);
}