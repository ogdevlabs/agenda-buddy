using Library.Entities;
using Library.Repositories;

namespace Provider.Services;

public class ProviderService
{
    private readonly IRepository<ProviderEntity> _providerRepository;

    public ProviderService(IRepository<ProviderEntity> providerRepository)
    {
        _providerRepository = providerRepository;
    }

    public async Task<IEnumerable<ProviderEntity>> GetAllProviders()
    {
        return await _providerRepository.GetAllAsync();
    }
    
    public async Task<ProviderEntity> GetProviderById(string id)
    {
        return await _providerRepository.GetByIdAsync(id);
    }

    public async Task AddProvider(ProviderEntity provider)
    {
        await _providerRepository.InsertAsync(provider);
    }

    public async Task UpdateProvider(string id, ProviderEntity provider)
    {
        var existingProvider = await _providerRepository.GetByIdAsync(id);
        if (existingProvider == null)
        {
            throw new ArgumentException("Provider not found");
        }
        await _providerRepository.UpdateAsync(id, provider);
    }

    public async Task DeleteProvider(string id)
    {
        await _providerRepository.DeleteAsync(id);
    }
}