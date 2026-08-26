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

    /// <summary>
    /// Applies <paramref name="update"/> to the single document matching <paramref name="filter"/>,
    /// atomically, and returns the document <b>as it is after the update</b>.
    /// </summary>
    /// <param name="filter">
    /// The match condition. It is also the concurrency guard: anything that must still be true at the
    /// moment of the write belongs here, not in a preceding read.
    /// </param>
    /// <param name="update">
    /// A MongoDB update document — <c>$set</c>, <c>$unset</c>, <c>$inc</c>. Never a whole replacement
    /// document.
    /// </param>
    /// <returns>The post-update document, or <c>null</c> when the filter matched nothing.</returns>
    /// <remarks>
    /// <para>
    /// F-021's one new primitive (ADR-032). It exists because <c>RefreshAsync</c> rotated a refresh
    /// token by <b>deleting the whole credential document and re-inserting it</b>, so any fault between
    /// the two lines destroyed the account — and no primitive here could express "change this one
    /// field". <c>UpdateAsync</c> replaces the entire document, which is a different operation with a
    /// different failure mode.
    /// </para>
    /// <para>
    /// <b>It never upserts.</b> A filter that matches nothing writes nothing and returns <c>null</c>.
    /// That is a property of the primitive rather than of each call site, so counting a failed login
    /// for an address that has no account cannot create one (F-021 AC-9).
    /// </para>
    /// <para>
    /// <b>Post-image, deliberately.</b> Returning the updated document lets a caller act on the new
    /// value without a second read — the failed-attempt counter is incremented and the returned count
    /// decides whether a lock is applied, in one round trip.
    /// </para>
    /// <para>
    /// PRD requirement 3 forbids growing this into a query builder. <c>BsonDocument</c> in and out is
    /// what keeps that promise: it is the same shape <see cref="FindOneAsync"/>,
    /// <see cref="FindOneAndDeleteAsync"/> and <see cref="FindAllAsync"/> already expose, so this adds
    /// no new abstraction style and stops at the driver boundary.
    /// </para>
    /// </remarks>
    Task<TEntity?> FindOneAndUpdateAsync(BsonDocument filter, BsonDocument update);
}
