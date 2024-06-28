namespace Library.Entities;

[ExcludeFromCodeCoverage]
public class CustomerEntity
{
    public CustomerEntity(ObjectId id, string? firstName, string? lastName, string? email)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
    }

    public CustomerEntity()
    {
        
    }

    [BsonElement("_id")] public ObjectId Id { get; set; }
    [Required] [BsonElement("first_name")] public string? FirstName { get; set; }
    [Required] [BsonElement("last_name")] public string? LastName { get; set; }

    [BsonElement("email")]
    [EmailAddress]
    [Required]
    public string? Email { get; set; }

    [BsonElement("providers")] public List<ProviderEntity>? ProviderCollection { get; set; } = [];
    [BsonElement("appointments")] public List<AppointmentEntity>? AppointmentCollection { get; set; } = [];

    
}