using System.Text.Json;
using AgendaBuddy.MobileApp.Infrastructure;

namespace AgendaBuddy.MobileApp.Services;

public sealed record PendingRegistration(
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Role);

public interface IPendingRegistrationStore
{
    Task SaveAsync(PendingRegistration registration);
    Task<PendingRegistration?> GetAsync();
    void Clear();
}

public class PendingRegistrationStore(ISecureStorageService secureStorage) : IPendingRegistrationStore
{
    private const string StorageKey = "pending_registration";

    public Task SaveAsync(PendingRegistration registration) =>
        secureStorage.SetAsync(StorageKey, JsonSerializer.Serialize(registration));

    public async Task<PendingRegistration?> GetAsync()
    {
        var value = await secureStorage.GetAsync(StorageKey);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : JsonSerializer.Deserialize<PendingRegistration>(value);
    }

    public void Clear() => secureStorage.Remove(StorageKey);
}