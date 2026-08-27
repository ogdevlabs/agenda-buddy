namespace AgendaBuddy.Library.Services;

public interface INoteService
{
    Task<NoteEntity> CreateAsync(NoteEntity note);
    Task<IEnumerable<NoteEntity>> GetByAppointmentAsync(string providerEmail, string appointmentIdentifier);
    Task<NoteEntity?> GetByIdAsync(string id);
    Task<NoteEntity> UpdateAsync(string id, string providerEmail, string content);
    Task DeleteAsync(string id, string providerEmail);
}
