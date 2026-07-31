#if MOBILE
using Microsoft.Maui.Storage;
#endif

namespace MobileApp.Infrastructure;

public class MauiSecureStorageService : ISecureStorageService
{
    public Task<string?> GetAsync(string key)
    {
#if MOBILE
        return SecureStorage.Default.GetAsync(key);
#else
        return Task.FromResult<string?>(null);
#endif
    }

    public Task SetAsync(string key, string value)
    {
#if MOBILE
        return SecureStorage.Default.SetAsync(key, value);
#else
        return Task.CompletedTask;
#endif
    }

    public void Remove(string key)
    {
#if MOBILE
        SecureStorage.Default.Remove(key);
#endif
    }
}
