namespace AgendaBuddy.Library.Services;

public class DeviceTokenService(IRepository<DeviceTokenEntity> repository) : IDeviceTokenService
{
    public async Task UpsertAsync(string userEmail, string token, string platform)
    {
        var existing = await GetByEmailAsync(userEmail);
        var now = DateTime.UtcNow;

        if (existing is not null)
        {
            existing.Token = token;
            existing.Platform = platform;
            existing.UpdatedAt = now;
            await repository.UpdateAsync(existing.Id, existing);
            return;
        }

        var entity = new DeviceTokenEntity
        {
            UserEmail = userEmail,
            Token = token,
            Platform = platform,
            RegisteredAt = now,
            UpdatedAt = now
        };
        await repository.InsertAsync(entity);
    }

    /// <remarks>
    /// Matched in the database. This used to read the whole collection and filter in memory, which was
    /// survivable only while nothing called it — it is now on the path of every notification that goes out.
    /// </remarks>
    public async Task<DeviceTokenEntity?> GetByEmailAsync(string userEmail)
    {
        return await repository.FindOneAsync(new BsonDocument("user_email", userEmail));
    }
}
