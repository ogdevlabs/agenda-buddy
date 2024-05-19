using Library.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Library.Tools;

public static class SupportTools<TEntity> where TEntity : class
{
    public static BsonDocument FilterByNameAndLastName(string firstName, string lastName)
    {
        return new BsonDocument("first_name", firstName).Add("last_name", lastName);
    }

    public static BsonDocument FilterByEmail(string email)
    {
        return new BsonDocument("email", email);
    }
}