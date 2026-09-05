namespace AgendaBuddy.Library.Repositories;

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
    /// The newest <paramref name="limit"/> documents matching <paramref name="filter"/>, in
    /// <paramref name="sort"/> order.
    /// </summary>
    /// <param name="sort">A MongoDB sort document, e.g. <c>{ created_at: -1 }</c>.</param>
    /// <param name="limit">Maximum documents to return. Values below one are treated as one.</param>
    /// <remarks>
    /// The sorted, bounded sibling of <see cref="FindAllAsync(BsonDocument)"/>, which returns every match in
    /// the database's natural order — so a caller that wants newest-first gets it by accident until something
    /// deletes a document, and an account's whole history on every read. Both the order and the bound have to
    /// happen in the database: sorting a full read in memory reintroduces the unbounded load.
    /// <para>
    /// Deliberately not a page: an inbox reads the newest N, it does not walk backwards through history, and
    /// <see cref="GetPagedAsync(BsonDocument,int,int)"/> already covers the case that does. Clamping the
    /// caller's <paramref name="limit"/> is the endpoint's job (ADR-023: clamp, never reject), as it is there.
    /// </para>
    /// </remarks>
    Task<IEnumerable<TEntity>> FindAllAsync(BsonDocument filter, BsonDocument sort, int limit);

    /// <summary>
    /// Applies <paramref name="update"/> to every document matching <paramref name="filter"/>, and returns
    /// how many it changed.
    /// </summary>
    /// <param name="update">A MongoDB update document. Never a whole replacement document.</param>
    /// <returns>The number of documents actually modified — zero when the filter matched nothing.</returns>
    /// <remarks>
    /// The many-document sibling of <see cref="FindOneAndUpdateAsync"/>, and like it, <b>it never upserts</b>.
    /// It exists so a bulk state change (mark every unread notification read) is one round trip and one
    /// atomic write per document, instead of a read of N followed by N replacements — the shape ADR-032 exists
    /// to remove. There is no post-image: a caller that needs the documents can read them.
    /// </remarks>
    Task<long> UpdateManyAsync(BsonDocument filter, BsonDocument update);

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
    /// ADR-023's repository half. One primitive rather than a query abstraction: the
    /// requirement is two paginated list endpoints, not a query DSL. <c>TotalCount</c> is
    /// <see cref="long"/> because <c>CountDocumentsAsync</c> returns <see cref="long"/>, and
    /// <c>api-contracts.md</c> §4 publishes <c>totalCount</c> as <see cref="long"/> for the
    /// same reason.
    /// <para>
    /// Capping and clamping the caller's page size is the <b>endpoint's</b> job (ADR-023: clamp,
    /// never reject), not this method's. The page cap is a security control — an uncapped page size
    /// restores the full-dataset dump this primitive exists to remove — so it belongs where the untrusted
    /// value arrives.
    /// </para>
    /// </remarks>
    Task<(IEnumerable<TEntity> Items, long TotalCount)> GetPagedAsync(int skip, int take);

    /// <summary>
    /// One page of the documents matching <paramref name="filter"/>, plus how many match in total.
    /// </summary>
    /// <remarks>
    /// The filtered sibling of <see cref="GetPagedAsync(int,int)"/>. It exists because filtering after
    /// paging is wrong in two ways at once — a page of 25 silently returns fewer, and
    /// <c>TotalCount</c> counts documents the caller is never shown — while filtering a full read and
    /// paging in memory reintroduces exactly the full-dataset load ADR-023 exists to remove. Both the
    /// match and the page therefore have to happen in the database.
    /// </remarks>
    Task<(IEnumerable<TEntity> Items, long TotalCount)> GetPagedAsync(BsonDocument filter, int skip, int take);

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
    /// ADR-032. It exists because <c>RefreshAsync</c> rotated a refresh
    /// token by <b>deleting the whole credential document and re-inserting it</b>, so any fault between
    /// the two lines destroyed the account — and no primitive here could express "change this one
    /// field". <c>UpdateAsync</c> replaces the entire document, which is a different operation with a
    /// different failure mode.
    /// </para>
    /// <para>
    /// <b>It never upserts.</b> A filter that matches nothing writes nothing and returns <c>null</c>.
    /// That is a property of the primitive rather than of each call site, so counting a failed login
    /// for an address that has no account cannot create one (AC-9).
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
