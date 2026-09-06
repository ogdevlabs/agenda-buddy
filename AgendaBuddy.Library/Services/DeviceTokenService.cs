namespace AgendaBuddy.Library.Services;

public class DeviceTokenService(IRepository<DeviceTokenEntity> repository) : IDeviceTokenService
{
    /// <inheritdoc/>
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
        }
        else
        {
            await repository.InsertAsync(new DeviceTokenEntity
            {
                UserEmail = userEmail,
                Token = token,
                Platform = platform,
                RegisteredAt = now,
                UpdatedAt = now
            });
        }

        await EvictFromOtherAccountsAsync(userEmail, token);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteByEmailAsync(string userEmail)
    {
        if (string.IsNullOrWhiteSpace(userEmail)) return false;

        return await repository.FindOneAndDeleteAsync(new BsonDocument("user_email", userEmail)) is not null;
    }

    /// <summary>
    /// Takes this token away from every account other than the one that just claimed it.
    /// </summary>
    /// <remarks>
    /// The invariant is that a device token addresses at most one account. It is enforced here, on the write
    /// that claims the token, rather than only on sign-out — sign-out is the path a user can simply not take,
    /// by quitting the app or handing the phone over, and the stale row survives that.
    /// <para>
    /// Ordered after this account's own write, so a fault between the two leaves the previous holder addressable
    /// rather than leaving nobody addressable. Rows are read then deleted by id because <c>IRepository</c> has
    /// no delete-many primitive; the match is on a single token, so the set is one row in practice.
    /// </para>
    /// </remarks>
    private async Task EvictFromOtherAccountsAsync(string userEmail, string token)
    {
        var stale = await repository.FindAllAsync(new BsonDocument
        {
            { "token", token },
            { "user_email", new BsonDocument("$ne", userEmail) }
        });

        foreach (var row in stale)
            await repository.DeleteAsync(row.Id);
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
