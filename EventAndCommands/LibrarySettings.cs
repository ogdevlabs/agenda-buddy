namespace EventAndCommands;

public class LibrarySettings
{
    public MongoDbSettings? MongoDbSettings { get; init; }
}

public class MongoDbSettings
{
    public string? ConnectionString { get; init; }
    public string? DatabaseName { get; init; }
    public string? CollectionName { get; init; }
} 