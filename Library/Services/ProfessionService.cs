namespace Library.Services;

public class ProfessionService(IRepository<ProfessionEntity> professionRepository) : IProfessionService
{
    public async Task CreateProfessionAsync(ProfessionEntity professionEntity)
    {
        await professionRepository.InsertAsync(professionEntity);
    }

    public async Task<IEnumerable<ProfessionEntity>> GetProfessionsAsync()
    {
        return await professionRepository.GetAllAsync();
    }

    public async Task<ProfessionEntity> GetProfessionAsync(string name)
    {
        var filterByName = SupportTools<ProfessionEntity>.FilterByName(name);
        var profession = await professionRepository.Find(filterByName);
        if (profession == null) return null!;
        return profession;
    }
}