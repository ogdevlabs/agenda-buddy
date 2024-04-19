using System.Data;
using Provider.Models;

namespace Provider.Infrastructure.Data;

public class DataSeeder
{
    public static void Seed(ProviderContext context)
    {
        if (context == null) throw new DataException("Cannot connect to DB");
        if (!context.Providers!.Any())
        {
            context.Providers!.AddRange(GetPreAddedProviders());
            context.SaveChanges();
        }
    }

    private static IEnumerable<ProviderModel> GetPreAddedProviders()
    {
        return new List<ProviderModel>()
        {
            new()
            {
                FirstName = "Profesor",
                LastName = "Jirafales",
                Email = "profesor.jirafales@elchavo.com",
                Phone = "(612)262-7624",
                AddressInformation = new Address()
                {
                    AddressLine1 = "La vecindad del Chavo",
                    AddressLine2 = "2",
                    City = "Somewhere",
                    State = "Somewhere",
                    ZipCode = "12345"
                }
            }
        };
    }
}