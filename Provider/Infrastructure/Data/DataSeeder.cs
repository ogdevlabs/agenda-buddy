using System.Data;
using Library.Entities;
using Library.Repositories;
using Provider.Configurations;
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

    public static void SeedDocument(IConfiguration configuration, string database, string collection)
    {
        var client = new MongoDbConfiguration(configuration).MongoClient();
        var repository = new MongoDbRepository<ProviderEntity>(client, 
            database,
            collection);
        var record = AddTestProviders().FirstOrDefault();
        repository.InsertAsync(record!).Wait();
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
                Topic = "profesor.jirafales-topic",
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

    private static IEnumerable<ProviderEntity> AddTestProviders()
    {
        return new List<ProviderEntity>()
        {
            new()
            {
                FirstName = "Professor",
                LastName = "Jirafales",
                Email = "professor.jirafales@elchavo.com",
                KafkaTopic = "TBD"
            }
        };
    }
}