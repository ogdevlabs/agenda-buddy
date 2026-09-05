namespace AgendaBuddy.Library.Services;

public class NotificationService(IRepository<NotificationEntity> repository) : INotificationService
{
    /// <summary>Rows returned when a caller names no limit.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// The hard ceiling on one read, applied in the service and not only at the endpoint. An inbox that
    /// returns everything is an unbounded read whose cost grows with the age of the account, and the
    /// screen that consumes it shows the newest handful.
    /// </summary>
    public const int MaxPageSize = 200;

    /// <summary>Newest first. Without it the driver answers in natural order, which is newest-first only until something deletes a document.</summary>
    private static readonly BsonDocument NewestFirst = new("created_at", -1);

    public async Task SendAsync(NotificationEntity notification)
    {
        notification.Id = ObjectId.GenerateNewId();
        await repository.InsertAsync(notification);
    }

    public async Task<IEnumerable<NotificationEntity>> GetForRecipientAsync(
        string recipientEmail, int limit = DefaultPageSize, bool unreadOnly = false)
    {
        var effectiveLimit = limit <= 0 ? DefaultPageSize : Math.Min(limit, MaxPageSize);
        return await repository.FindAllAsync(FilterFor(recipientEmail, unreadOnly), NewestFirst, effectiveLimit);
    }

    // GetPagedAsync's TotalCount is the count of everything matching the filter, not the size of the page it
    // returns -- so a page of one is the cheapest way to ask the database to count without reading the rows.
    public async Task<long> CountUnreadAsync(string recipientEmail) =>
        (await repository.GetPagedAsync(FilterFor(recipientEmail, unreadOnly: true), 0, 1)).TotalCount;

    /// <remarks>
    /// A targeted <c>$set</c> (ADR-032), not a read followed by <c>UpdateAsync</c> — which replaces the whole
    /// document and would let a concurrent write to any other field be lost to a read that predated it.
    /// <c>is_read: false</c> is part of the filter rather than a preceding check, so marking an
    /// already-read notification writes nothing.
    /// </remarks>
    public async Task MarkReadAsync(string notificationId)
    {
        if (!ObjectId.TryParse(notificationId, out var objectId)) return;

        await repository.FindOneAndUpdateAsync(
            new BsonDocument { { "_id", objectId }, { "is_read", false } },
            new BsonDocument("$set", new BsonDocument("is_read", true)));
    }

    /// <remarks>
    /// One multi-document <c>$set</c> rather than N read-modify-writes. Scoped by recipient in the filter, so
    /// it cannot reach another account's rows even if a caller passes an address it does not own — the route
    /// still takes the address from the caller's own claim rather than the request.
    /// </remarks>
    public async Task<long> MarkAllReadAsync(string recipientEmail) =>
        await repository.UpdateManyAsync(
            FilterFor(recipientEmail, unreadOnly: true),
            new BsonDocument("$set", new BsonDocument("is_read", true)));

    private static BsonDocument FilterFor(string recipientEmail, bool unreadOnly)
    {
        var filter = new BsonDocument("recipient_email", recipientEmail);
        if (unreadOnly) filter.Add("is_read", false);
        return filter;
    }
}
