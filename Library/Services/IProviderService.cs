namespace Library.Services;

public interface IProviderService
{
    Task<IEnumerable<ProviderEntity>> GetAllProvidersAsync();
    Task<ProviderEntity> GetProviderByIdAsync(string id);
    Task AddProviderAsync(ProviderEntity provider);
    Task<bool> UpdateProviderAsync(string id, ProviderEntity provider);
    Task DeleteProviderAsync(string id);
    Task<ProviderEntity> FindProvidersAsync(BsonDocument filter);
}
