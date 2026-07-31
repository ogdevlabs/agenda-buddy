namespace Library.Services;

public class NoteService(IRepository<NoteEntity> repository) : INoteService
{
    public async Task<NoteEntity> CreateAsync(NoteEntity note)
    {
        note.Id = ObjectId.GenerateNewId();
        note.CreatedAt = DateTime.UtcNow;
        note.UpdatedAt = DateTime.UtcNow;
        await repository.InsertAsync(note);
        return note;
    }

    public async Task<IEnumerable<NoteEntity>> GetByAppointmentAsync(string providerEmail, string appointmentIdentifier)
    {
        var filter = new BsonDocument
        {
            { "provider_email", providerEmail },
            { "appointment_identifier", appointmentIdentifier }
        };
        return await repository.FindAllAsync(filter);
    }

    public async Task<NoteEntity?> GetByIdAsync(string id)
    {
        var note = await repository.GetByIdAsync(id);
        return note;
    }

    public async Task<NoteEntity> UpdateAsync(string id, string providerEmail, string content)
    {
        var note = await repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Note {id} not found.");

        if (!string.Equals(note.ProviderEmail, providerEmail, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Only the owning provider may update this note.");

        note.Content = content;
        note.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(id, note);
        return note;
    }

    public async Task DeleteAsync(string id, string providerEmail)
    {
        var note = await repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Note {id} not found.");

        if (!string.Equals(note.ProviderEmail, providerEmail, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Only the owning provider may delete this note.");

        await repository.DeleteAsync(id);
    }
}
