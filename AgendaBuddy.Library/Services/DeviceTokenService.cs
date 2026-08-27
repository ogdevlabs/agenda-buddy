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

    public async Task<DeviceTokenEntity?> GetByEmailAsync(string userEmail)
    {
        var all = await repository.GetAllAsync();
        return all.FirstOrDefault(x => x.UserEmail == userEmail);
    }
}
