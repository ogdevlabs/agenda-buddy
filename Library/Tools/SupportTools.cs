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

    public static List<ServiceEntity> GenerateIdForRecord(List<ServiceEntity> dataCollection)
    {
        foreach (var service in dataCollection)
        {
            service.Id = ObjectId.GenerateNewId();
        }

        return dataCollection;
    }
}