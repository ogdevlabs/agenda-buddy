namespace AgendaBuddy.Library.Services;

public interface IDeviceTokenService
{
    /// <summary>
    /// Records this account's device token, and takes the token away from any other account holding it.
    /// </summary>
    /// <remarks>
    /// A device token identifies a <b>device</b>, so at most one account may be addressable through it. Without
    /// the eviction, signing in as B on a device that had been signed in as A left A's row pointing at the same
    /// token — and every notification for A, subject and body included, was pushed to a device A no longer
    /// controls.
    /// </remarks>
    Task UpsertAsync(string userEmail, string token, string platform);

    Task<DeviceTokenEntity?> GetByEmailAsync(string userEmail);

    /// <summary>
    /// Removes this account's device registration, so nothing further is pushed to it.
    /// </summary>
    /// <returns><c>true</c> when a registration was removed, <c>false</c> when there was none.</returns>
    /// <remarks>
    /// Signing out is the point at which a device stops being this account's. <see cref="UpsertAsync"/>'s
    /// eviction only fires when somebody else signs in on that device, which may be never — so without this,
    /// the account that signed out keeps receiving push on a device it has walked away from.
    /// </remarks>
    Task<bool> DeleteByEmailAsync(string userEmail);
}
