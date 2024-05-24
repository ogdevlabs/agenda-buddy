using Library.Entities;

namespace Services.Services;

public interface IMongoDbService
{ 
    Task<List<ServiceEntity>> GetServices();
}