namespace AgendaBuddy.Library.Services;

public interface IDeviceTokenService
{
    Task UpsertAsync(string userEmail, string token, string platform);
    Task<DeviceTokenEntity?> GetByEmailAsync(string userEmail);
}
