using Library.Entities;
using Library.Repositories;
using MongoDB.Bson;

namespace Identity.Tests.Helpers;

/// <summary>
/// In-memory IRepository<CredentialEntity> for IdentityService unit tests.
/// Supports FindOneAsync by simple field predicate and FindOneAndDeleteAsync.
/// </summary>
public class InMemoryCredentialRepository : IRepository<CredentialEntity>
{
    private readonly List<CredentialEntity> _store = new();

    public Task<IEnumerable<CredentialEntity>> GetAllAsync() =>
        Task.FromResult<IEnumerable<CredentialEntity>>(_store.ToList());

    public Task<CredentialEntity> GetByIdAsync(string id) =>
        Task.FromResult(_store.FirstOrDefault(e => e.Id == id)!);

    public Task InsertAsync(CredentialEntity entity)
    {
        _store.Add(entity);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(string id, CredentialEntity entity)
    {
        var idx = _store.FindIndex(e => e.Id == id);
        if (idx < 0) return Task.FromResult(false);
        _store[idx] = entity;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateByIdentifierAsync(string identifier, CredentialEntity entity) =>
        Task.FromResult(false);

    public Task<bool> DeleteAsync(string id)
    {
        var item = _store.FirstOrDefault(e => e.Id == id);
        if (item is null) return Task.FromResult(false);
        _store.Remove(item);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteByIdentifierAsync(string identifier) =>
        Task.FromResult(false);

    public Task<CredentialEntity> Find(BsonDocument filter) =>
        Task.FromResult(FindByFilter(filter)!);

    public Task<CredentialEntity?> FindOneAsync(BsonDocument filter) =>
        Task.FromResult(FindByFilter(filter));

    public Task<CredentialEntity?> FindOneAndDeleteAsync(BsonDocument filter)
    {
        var item = FindByFilterWithExpiry(filter);
        if (item is not null) _store.Remove(item);
        return Task.FromResult(item);
    }

    public Task<IEnumerable<CredentialEntity>> FindAllAsync(BsonDocument filter) =>
        Task.FromResult<IEnumerable<CredentialEntity>>(_store.Where(e =>
            MatchesFilter(e, filter)).ToList());

    // ADR-023's repository half (F-016-T10). Negatives are normalised to zero to match
    // MongoDbRepository, where Skip(-1) throws rather than being the no-op LINQ makes it.
    public Task<(IEnumerable<CredentialEntity> Items, long TotalCount)> GetPagedAsync(int skip, int take) =>
        Task.FromResult<(IEnumerable<CredentialEntity>, long)>((
            _store.Skip(Math.Max(0, skip)).Take(Math.Max(0, take)).ToList(),
            _store.Count));

    // Simple filter evaluation for test purposes
    private CredentialEntity? FindByFilter(BsonDocument filter)
    {
        return _store.FirstOrDefault(e => MatchesFilter(e, filter));
    }

    private CredentialEntity? FindByFilterWithExpiry(BsonDocument filter)
    {
        // Supports: {"refresh_token.hash": hash, "refresh_token.expiry": {$gt: utcNow}}
        string? hashFilter = null;
        DateTime? expiryGt = null;

        foreach (var elem in filter)
        {
            if (elem.Name == "refresh_token.hash")
                hashFilter = elem.Value.AsString;
            else if (elem.Name == "refresh_token.expiry" && elem.Value.IsBsonDocument)
            {
                var doc = elem.Value.AsBsonDocument;
                if (doc.Contains("$gt"))
                    expiryGt = doc["$gt"].ToUniversalTime();
            }
        }

        return _store.FirstOrDefault(e =>
            (hashFilter is null || e.RefreshToken?.Hash == hashFilter) &&
            (expiryGt is null || (e.RefreshToken?.Expiry > expiryGt)));
    }

    private static bool MatchesFilter(CredentialEntity e, BsonDocument filter)
    {
        foreach (var elem in filter)
        {
            switch (elem.Name)
            {
                case "email" when e.Email != elem.Value.AsString: return false;
                case "refresh_token.hash" when e.RefreshToken?.Hash != elem.Value.AsString: return false;
            }
        }
        return true;
    }
}
