namespace Library.Services;

public class ProfessionService(IRepository<ProfessionEntity> professionRepository) : IProfessionService
{
    public async Task<ProfessionEntity> GetProfessionAsync(string name)
    {
        var filterByName = new BsonDocument("name", name);
        var profession = await professionRepository.Find(filterByName);
        if (profession == null) return null!;
        return profession;
    }

    public async Task CreateProfessionAsync(ProfessionEntity professionEntity)
    {
        if (GetProfessionAsync(professionEntity.Name) == null)
        {
            await professionRepository.InsertAsync(professionEntity);
        }
    }
}