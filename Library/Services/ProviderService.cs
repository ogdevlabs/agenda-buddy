namespace Library.Services;

public class ProviderService(IRepository<ProviderEntity> providerRepository) : IProviderService
{
    public async Task<IEnumerable<ProviderEntity>> GetAllProvidersAsync()
    {
        return await providerRepository.GetAllAsync();
    }

    /// <summary>
    /// One page of providers, plus the total number of them. F-016-T15 / ADR-023.
    /// </summary>
    /// <remarks>
    /// Paged at the database, not after the fact. Reading everything and slicing in the endpoint would bound
    /// the RESPONSE while leaving the EXTRACTION unbounded, which is the opposite of the point.
    /// </remarks>
    public async Task<(IEnumerable<ProviderEntity> Items, long TotalCount)> GetPagedProvidersAsync(int skip, int take)
    {
        return await providerRepository.GetPagedAsync(skip, take);
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