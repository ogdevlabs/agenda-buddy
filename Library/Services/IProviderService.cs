namespace Library.Services;

public interface IProviderService
{
    Task<IEnumerable<ProviderEntity>> GetAllProviders();
    Task<ProviderEntity> GetProviderById(string id);
    Task AddProvider(ProviderEntity provider);
    Task<bool> UpdateProvider(string id, ProviderEntity provider);
    Task DeleteProvider(string id);
    Task<ProviderEntity> FindProviders(BsonDocument filter);
}