namespace Services.Configurations;

public interface IMongoDbConfiguration
{
    public MongoClient MongoClient();
}
