using MongoDB.Driver;

namespace Calendar.Configurations;

public interface IMongoDbConfiguration
{
    public MongoClient MongoClient();
}