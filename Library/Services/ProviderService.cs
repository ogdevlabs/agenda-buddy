namespace Library.Services;

public class ProviderService(IRepository<ProviderEntity> providerRepository) : IProviderService
{
    public async Task<IEnumerable<ProviderEntity>> GetAllProvidersAsync()
    {
        return await providerRepository.GetAllAsync();
    }

    public async Task<ProviderEntity> GetProviderByIdAsync(string id)
    {
        return await providerRepository.GetByIdAsync(id);
    }

    public async Task AddProviderAsync(ProviderEntity provider)
    {
        await providerRepository.InsertAsync(provider);
    }

    public async Task<bool> UpdateProviderAsync(string id, ProviderEntity provider)
    {
        var existingProvider = await providerRepository.GetByIdAsync(id);
        if (existingProvider == null) throw new ArgumentException("Provider not found");

        return await providerRepository.UpdateAsync(id, provider);
    }

    public async Task DeleteProviderAsync(string id)
    {
        await providerRepository.DeleteAsync(id);
    }

    public async Task<ProviderEntity> FindProvidersAsync(BsonDocument filter)
    {
        return await providerRepository.Find(filter);
    }
}