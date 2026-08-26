namespace Library.Services;

public interface IProfessionService
{
    public Task<ProfessionEntity> GetProfessionAsync(string name);
    public Task CreateProfessionAsync(ProfessionEntity professionEntity);
    public Task<List<ProfessionEntity>> GetProfessionCollectionAsync();
}
