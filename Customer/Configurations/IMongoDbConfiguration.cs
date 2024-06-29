namespace Customer.Configurations;

public interface IMongoDbConfiguration
{
    public MongoClient MongoClient();
}