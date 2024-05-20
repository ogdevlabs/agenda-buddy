using Library.Entities;
using Library.Repositories;
using MongoDB.Bson;

namespace Library.Services;

public class ProviderService (IRepository<ProviderEntity> providerRepository)
{
    public async Task<IEnumerable<ProviderEntity>> GetAllProviders()
    {
        return await providerRepository.GetAllAsync();
    }
    
    public async Task<ProviderEntity> GetProviderById(string id)
    {
        return await providerRepository.GetByIdAsync(id);
    }

    public async Task AddProvider(ProviderEntity provider)
    {
        await providerRepository.InsertAsync(provider);
    }

    public async Task UpdateProvider(string id, ProviderEntity provider)
    {
        var existingProvider = await providerRepository.GetByIdAsync(id);
        if (existingProvider == null)
        {
            throw new ArgumentException("Provider not found");
        }
        await providerRepository.UpdateAsync(id, provider);
    }

    public async Task DeleteProvider(string id)
    {
        await providerRepository.DeleteAsync(id);
    }

    public async Task<ProviderEntity> FindProviders(BsonDocument filter)
    {
        return await providerRepository.Find(filter);
    }
}