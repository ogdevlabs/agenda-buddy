using Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Services.Services;

public class MongoDbService : IMongoDbService
{
    private readonly IMongoCollection<Service> _collection;

    public MongoDbService(IConfiguration configuration)
    {
        var mongoUri = configuration.GetSection("MongoDB")["ConnectionString"]!.ToString();
        //settings.ServerApi = new ServerApi(ServerApiVersion.V1);
        var client = new MongoClient(mongoUri);
        var database = client.GetDatabase("provider-service");
        _collection = database.GetCollection<Service>("services");
    }
    public async Task<List<Service>> GetServices()
    {
        return await _collection.Find(service => true).ToListAsync();
    }
}