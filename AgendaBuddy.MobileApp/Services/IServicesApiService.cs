using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

/// <summary>A provider's own service catalogue (api-contracts.md — "Services catalog").</summary>
public interface IServicesApiService
{
    Task<List<ServiceItem>> GetServicesAsync(string email, CancellationToken ct = default);

    /// <summary>Appends new services — never send an id, the server generates one.</summary>
    Task<bool> AddServicesAsync(string email, List<ServiceItem> newServices, CancellationToken ct = default);

    /// <summary>Updates existing services, matched by <see cref="ServiceItem.Name"/> server-side.</summary>
    Task<bool> UpdateServicesAsync(string email, List<ServiceItem> updatedServices, CancellationToken ct = default);

    /// <summary>Removes one service, matched by name server-side.</summary>
    Task<bool> RemoveServiceAsync(string email, string name, CancellationToken ct = default);
}
