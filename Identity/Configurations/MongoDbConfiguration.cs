namespace Identity.Configurations;

public class MongoDbConfiguration(IConfiguration configuration) : IMongoDbConfiguration
{
    public MongoClient MongoClient()
    {
        return new MongoClient(configuration.GetSection("MongoDbSettings")["ConnectionString"]!);
    }
}
