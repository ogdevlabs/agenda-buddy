namespace Profession.Configurations;

public class MongoDbConfiguration(IConfiguration configuration) : IMongoDbConfiguration
{
    public MongoClient MongoClient()
    {
        return new MongoClient(configuration.GetSection("MongoDB")["ConnectionString"]!);
    }
}