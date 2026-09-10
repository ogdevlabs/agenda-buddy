using MongoDB.Bson.Serialization.Attributes;

namespace AgendaBuddy.Booking.Api.Payments;

public sealed class StripeWebhookEventStore
{
    private readonly IMongoCollection<StripeWebhookEvent> _events;

    public StripeWebhookEventStore(IMongoClient client, IConfiguration configuration)
    {
        var databaseName = MongoConnectionResolver.ResolveSetting(configuration, "DatabaseName", "agenda_buddy");
        _events = client.GetDatabase(databaseName).GetCollection<StripeWebhookEvent>("stripe_webhook_events");
    }

    public async Task<bool> TryBeginAsync(string eventId, string type, DateTime createdAt)
    {
        var now = DateTime.UtcNow;
        try
        {
            await _events.InsertOneAsync(new StripeWebhookEvent(eventId, type, createdAt, now, null));
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var claimed = await _events.FindOneAndUpdateAsync(
                Builders<StripeWebhookEvent>.Filter.And(
                    Builders<StripeWebhookEvent>.Filter.Eq(value => value.Id, eventId),
                    Builders<StripeWebhookEvent>.Filter.Eq(value => value.CompletedAt, null),
                    Builders<StripeWebhookEvent>.Filter.Lt(value => value.ProcessingStartedAt, now.AddMinutes(-5))),
                Builders<StripeWebhookEvent>.Update.Set(value => value.ProcessingStartedAt, now));
            return claimed is not null;
        }
    }

    public Task MarkCompletedAsync(string eventId) => _events.UpdateOneAsync(
        Builders<StripeWebhookEvent>.Filter.Eq(value => value.Id, eventId),
        Builders<StripeWebhookEvent>.Update.Set(value => value.CompletedAt, DateTime.UtcNow));

    public async Task EnsurePaymentIndexesAsync()
    {
        var payments = _events.Database.GetCollection<PaymentEntity>("payments");
        await payments.Indexes.CreateOneAsync(new CreateIndexModel<PaymentEntity>(
            Builders<PaymentEntity>.IndexKeys.Ascending(payment => payment.AppointmentIdentifier),
            new CreateIndexOptions { Unique = true, Name = "ux_payment_appointment" }));
    }
}

public sealed record StripeWebhookEvent(
    [property: BsonId] string Id,
    [property: BsonElement("type")] string Type,
    [property: BsonElement("created_at"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] DateTime CreatedAt,
    [property: BsonElement("processing_started_at"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)] DateTime ProcessingStartedAt,
    [property: BsonElement("completed_at"), BsonIgnoreIfNull, BsonDateTimeOptions(Kind = DateTimeKind.Utc)] DateTime? CompletedAt);
