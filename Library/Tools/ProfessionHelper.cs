using MongoDB.Driver.Linq;

namespace Library.Tools;

public abstract class ProfessionHelper
{
    public static List<ProfessionEntity> CleanUpDefaultProfessionEntities(List<ProfessionEntity> professionEntities)
    {
        return professionEntities.Where(att => !att.Name.Equals("string", StringComparison.CurrentCultureIgnoreCase))
            .ToList();
    }

    public static List<ProfessionEntity> RemoveDuplicateProfessionEntities(List<ProfessionEntity> professionEntities)
    {
        return professionEntities.GroupBy(att => att.Name).Select(group => group.First()).ToList();
    }
}