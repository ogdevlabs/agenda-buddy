namespace EventAndCommands.Persistence;

[ExcludeFromCodeCoverage]
public class Event
{
    [BsonElement("_id")] public ObjectId Id { get; set; }

    [BsonElement("timestamp")] public DateTime TimeStamp { get; set; }

    [BsonElement("status")] public string? Status { get; set; }

    [BsonElement("type")] public string? Type { get; set; }

    [BsonElement("data")] public string? Data { get; set; }
}