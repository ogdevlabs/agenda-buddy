using MongoDB.Driver;

namespace Provider.Configurations;

public interface IMongoDbConfiguration
{
    public MongoClient MongoClient();
}