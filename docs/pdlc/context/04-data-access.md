# 04 — Data Access

> **⚠️ F-013 delta (2026-08-18, `v0.1.0`) — this file was written 2026-08-15 and has NOT been re-read since.**
>
> **Stale.** All seven services and `EventStore` now share **one process-wide `IMongoClient` singleton**. Previously `EventStore` was Scoped and constructed a `MongoClient` — with its own connection pool and monitoring threads — **per HTTP request**, and every command and query handler writes an audit event, so this happened on every request. Connection strings resolve through `Library/MongoConnectionResolver.cs`, not a direct config-section read.
>
> `file:line` anchors below may have shifted. Authoritative sources for the change: `docs/pdlc/archive/design/aspire-wiring/ARCHITECTURE.md`, `docs/pdlc/episodes/EPISODE_aspire-wiring_2026-08-17.md`. A full targeted rehydration is queued as the first step of F-018.


**Files:** `Library/Repositories/IRepository.cs`, `Library/Repositories/MongoDbRepository.cs`, `Library/Tools/SupportTools.cs`, `Library/Tools/CacheAside.cs`, plus the seven per-service `MongoDbConfiguration.cs` classes.

`CONSTITUTION.md` §2: "Repository pattern only — no direct MongoDB queries outside `MongoDbRepository<T>`." **This constraint is violated in three places**, listed at the end.

---

## `IRepository<T>` (`Library/Repositories/IRepository.cs`)

Single generic interface, `where TEntity : class`, 11 members:

| Member | Line | Notes |
|---|---|---|
| `Task<IEnumerable<TEntity>> GetAllAsync()` | `:5` | Unbounded |
| `Task<TEntity> GetByIdAsync(string id)` | `:6` | ⚠️ non-nullable return but can yield `null` |
| `Task InsertAsync(TEntity entity)` | `:7` | |
| `Task<bool> UpdateAsync(string id, TEntity entity)` | `:8` | Full replace |
| `Task<bool> UpdateByIdentifierAsync(string identifier, TEntity entity)` | `:9` | Full replace on business key |
| `Task<bool> DeleteAsync(string id)` | `:10` | Hard delete |
| `Task<bool> DeleteByIdentifierAsync(string identifier)` | `:11` | Hard delete |
| `Task<TEntity> Find(BsonDocument filter)` | `:12` | ⚠️ non-nullable return, no `Async` suffix |
| `Task<TEntity?> FindOneAsync(BsonDocument filter)` | `:13` | Nullable — correct |
| `Task<TEntity?> FindOneAndDeleteAsync(BsonDocument filter)` | `:14` | Atomic read-and-remove |
| `Task<IEnumerable<TEntity>> FindAllAsync(BsonDocument filter)` | `:15` | Unbounded |

⚠️ **Nullability contract is inconsistent and wrong.** `GetByIdAsync` (`:6`) and `Find` (`:12`) declare non-nullable `TEntity` but their implementations use `FirstOrDefaultAsync()` (`MongoDbRepository.cs:30,71`) which returns `null` on a miss. Callers that trust the signature get an unguarded `NullReferenceException`; callers that check get a compiler nullable warning. Live examples:
- `CancelAppointmentCommandHandler.cs:52` — `appointment.EmailProvider` immediately after `SearchAppointmentAsync`, no null check.
- `NoteService.cs:32` — uses `?? throw new KeyNotFoundException(...)` on a *non-nullable* return, which the compiler flags as a redundant null check but is in fact necessary.
- `NotificationService.cs:20` / `MessageService.cs:37` — `if (x is null) return;` on a non-nullable return, same contradiction.

⚠️ **`Find` breaks the naming convention** (`CONSTITUTION.md` §2 "Methods: PascalCase + Async suffix for async"). It is async but unsuffixed, and it duplicates `FindOneAsync` exactly (`MongoDbRepository.cs:71` vs `:76` — identical bodies).

---

## `MongoDbRepository<T>` (`Library/Repositories/MongoDbRepository.cs`)

Two constructors:
- `:9` `(MongoClient dbClient, string databaseName, string collectionName)` — resolves the database itself.
- `:15` `(IMongoDatabase database, string collectionName)` — **the one every service actually uses** (all seven `ServiceCollectionExtension`s pass a pre-resolved `IMongoDatabase`).

⚠️ `using Library.Data;` at `:1` is unused.

### Query shapes

| Method | Line | Mongo call | Notes |
|---|---|---|---|
| `GetAllAsync` | `:22` | `Find(new BsonDocument()).ToListAsync()` | ⚠️ empty filter = **full collection scan**, no limit |
| `GetByIdAsync` | `:28-30` | `Builders<T>.Filter.Eq("_id", new ObjectId(id))` | ⚠️ `new ObjectId(id)` throws `FormatException` on a non-24-hex string — no `TryParse` |
| `InsertAsync` | `:36` | `InsertOneAsync` | No `InsertManyAsync` on the interface |
| `UpdateAsync` | `:41-44` | `ReplaceOneAsync` on `_id` | Returns `ModifiedCount > 0` |
| `UpdateByIdentifierAsync` | `:49-51` | `ReplaceOneAsync` on `identifier` | Same |
| `DeleteAsync` | `:56-59` | `DeleteOneAsync` on `_id` | |
| `DeleteByIdentifierAsync` | `:63-66` | `DeleteOneAsync` on `identifier` | |
| `Find` / `FindOneAsync` | `:71`, `:76` | `Find(filter).FirstOrDefaultAsync()` | Identical implementations |
| `FindOneAndDeleteAsync` | `:81` | `FindOneAndDeleteAsync` | Used only by `IdentityService.RefreshAsync` |
| `FindAllAsync` | `:86` | `Find(filter).ToListAsync()` | No limit |

⚠️ **`UpdateAsync`/`UpdateByIdentifierAsync` return `ModifiedCount > 0`, so a no-op update reads as failure.** Writing a document identical to the stored one yields `ModifiedCount == 0` → `false`. Callers treat that as "not found": `UpdateProviderCommandHandler.cs:24` skips the success audit event, and `Provider/Program.cs:188-190` returns `404 NotFound`. **Failure scenario:** a client PUTs an unchanged provider record and gets a 404. `MatchedCount` would be the correct predicate.

⚠️ **No projections anywhere.** Every read materialises the full document. For `ProviderEntity` — which embeds the whole appointment history and service catalogue (`05-data-model.md`) — `GetAllProvidersAsync()` on the anonymous `GET /api/v1/providers` route pulls every provider's entire nested graph into memory and serialises it to the wire.

⚠️ **No index definitions in application code.** The only index in the repo is created by the seed shell script: `scripts/seed/seed-mongo.sh:39` creates a unique index on `IdentityDb.credentials.email`. Every other query — `email`, `identifier`, `recipient_email`, `thread_id`, `provider_email`, `appointment_identifier`, `user_email`, `day_off`, `refresh_token.hash` — runs **unindexed** (collection scan) unless the indexes were created out of band. `[verify against live Atlas cluster]`.

⚠️ **No `IMongoClient` reuse across repositories per service is guaranteed but a client is constructed per *service* at startup outside DI** (`Booking/Extensions/ServiceCollectionExtension.cs:9`). That is acceptable (one `MongoClient` per process, which is the driver's recommendation), but because it happens at registration time rather than via DI, it cannot be replaced in tests without rebuilding the container.

### N+1 and scan risks

| Risk | Anchor | Description |
|---|---|---|
| Full-collection scan per login | `Library/Services/DeviceTokenService.cs:32` | `GetAllAsync()` then in-memory `FirstOrDefault` |
| Full-collection scan | `Library/Services/CalendarService.cs:7` | `GetAllAppointmentsAsync()` unfiltered |
| Cross-provider scan | `Library/Services/CalendarService.cs:17-18` | `day_off: false` with no provider scope |
| N writes in a loop | `Library/Services/CalendarService.cs:25-38` | One `InsertAsync` per blocked day |
| Read-modify-write ×2 | `EventAndCommands/Commands/Booking/BookingAppointmentCommandHandler.cs:51-54` | Insert appointment, then re-read it, then replace the whole provider document |
| Unbounded list serialisation | `Provider/Program.cs:132`, `Customer/Program.cs:146` | Anonymous endpoints returning every document |

---

## Filter construction — `SupportTools<TEntity>` (`Library/Tools/SupportTools.cs`)

A generic static helper whose type parameter is **unused by the filter methods** — `SupportTools<ProviderEntity>.FilterByEmail(x)` and `SupportTools<CustomerEntity>.FilterByEmail(x)` produce the identical `BsonDocument`. The generic parameter provides call-site documentation only.

| Helper | Line | Produces |
|---|---|---|
| `FilterByNameAndLastName` | `:5` | `{ first_name, last_name }` |
| `FilterByIdentifier` | `:10` | `{ identifier }` |
| `FilterByEmail` | `:15` | `{ email }` |
| `FilterByName` | `:20` | `{ name }` |
| `FilterByEmailProvider` | `:25` | `{ email_provider }` |
| `GenerateIdForRecord` | `:30` | Mutates a `List<ServiceEntity>`, assigning fresh `ObjectId`s |
| `GetThirtyDaysCalendarAvailability` | `:37` | Pure in-memory slot computation |

⚠️ **`FilterByEmail` is case-sensitive.** Mongo string equality is exact, but `IdentityService` normalises emails to lower case on register/login (`Identity/Services/IdentityService.cs:27,81`) while `ProviderEntity.Email` / `CustomerEntity.Email` are stored **as submitted**. A provider who registers `Sarah@x.com` gets credentials under `sarah@x.com`, and the JWT `sub` claim is the lower-cased form. `OwnershipGuard` compares case-insensitively (`Library.ServerAuth/Tools/OwnershipGuard.cs:10`) so the guard passes — but `SupportTools.FilterByEmail(user's sub)` then **fails to find the provider document**. Real cross-layer inconsistency.

⚠️ **`GenerateIdForRecord:32` mutates its input list in place** and returns it — used at `AddServicesToProviderCommandHandler.cs:23`, so the caller's list is silently modified.

### ⚠️ `GetThirtyDaysCalendarAvailability` (`:37-61`)

The actual availability algorithm. Hardcodes business hours **09:00–19:00** (`:47`, `:55`, `:66-67`).

- `:40-41` uses `DateTime.Today` and `:65` uses `DateTime.Now` — **local server time** — while every appointment is persisted as UTC (`[BsonDateTimeOptions(Kind = DateTimeKind.Utc)]`, `AppointmentEntity.cs:35,39`). ⚠️ The slot grid and the booked-slot set are therefore in **different time zones**; `bookedTimeSlots.Contains(slot)` at `:59` will mismatch by the UTC offset, so booked slots appear available.
- `:48-53` for *today*: `aux = GetTodayAvailableTime()` returns *remaining hours* (`:77`), then `aux = 19 - aux` converts it to a start hour. If 6 hours remain, the start hour becomes 13 — but 19 − 6 = 13 only holds when "now" is exactly 13:00 with the endpoint at 19:00. ⚠️ The arithmetic conflates a duration with a clock hour; correct would be `currentTime.Hour + 1`.
- `:71` returns 0 (no availability) once within 4 hours of close — an undocumented 4-hour booking lead time.
- ⚠️ **The provider's actual schedule is never consulted.** F-005 `provider-availability-schedule` shipped as "Provider sets their available hours/days", but there is **no availability/schedule field on `ProviderEntity`** (`05-data-model.md`) and no code reads one. Availability is universally 09:00–19:00 for every provider. Spec/code drift on a shipped feature.

---

## Caching — `CacheAside` (`Library/Tools/CacheAside.cs`)

An extension on `IDistributedCache`, mandated by `CONSTITUTION.md` §3 for all read-heavy queries. Used at 8 endpoint call sites (`Provider/Program.cs:137,157`; `Customer/Program.cs:151,165`; `Calendar/Program.cs:103,131`; `Profession/Program.cs:129,144`; `Services/Program.cs:103`).

Default TTL: 5 minutes absolute (`:8-11`), matching `CLAUDE.md`.

Serialisation: `System.Text.Json` string round-trip (`:23`, `:45`).

### Three defects

⚠️ **1. A single static semaphore for the entire process** (`:13`) — `private static readonly SemaphoreSlim Semaphore = new(1, 1)`. It is shared across **all cache keys and all entity types**. A cache miss on `providers` blocks a concurrent miss on `professions`, on `customers-alice@x.com`, and on every other key. Under load this serialises all cache-miss traffic in the process through one gate. Should be per-key.

⚠️ **2. Returns `default!` (i.e. `null`) on lock timeout** (`:30-34`) — if the 500 ms wait fails, the method returns null **without consulting the database**. The endpoint then interprets null as "no data":
- `Provider/Program.cs:143-146` → `204 NoContent`
- `Calendar/Program.cs:108-109` → `404 NotFound`
- **Failure scenario:** ~10 concurrent first-time requests for different providers. Defect 1 queues them on the shared semaphore; requests that wait >500 ms get `null`; the client receives **404/204 for data that exists**. This is a correctness bug caused by contention, and it degrades precisely when load rises.

⚠️ **3. The double-check is dead code** (`:38`) — `cachedValue = await cache.GetStringAsync(key, cancellationToken);` re-reads the cache inside the lock, then the result is **never examined**; `:39` unconditionally calls the factory. The classic double-checked-locking optimisation is written but not wired, so every queued waiter re-queries the database after the first one populated the cache.

⚠️ **4. `AddDistributedMemoryCache()` is what backs it** (`Provider/Program.cs:10`, `Calendar/Program.cs:9`, `Customer/Program.cs:9`, `Services/Program.cs:11`, `Profession/Program.cs:8`). Despite the `IDistributedCache` abstraction, the store is **in-process memory**. No Redis, no SQL cache, nothing shared. Scaling any service to two replicas gives two divergent caches with a 5-minute staleness window and no invalidation — and **nothing anywhere invalidates a cache key on write**. `POST /api/v1/providers` does not evict the `providers` key, so a newly created provider is invisible for up to 5 minutes.

---

## ⚠️ Violations of "no direct MongoDB access outside `MongoDbRepository<T>`"

1. **`EventAndCommands/Persitency/EventStore.cs:9-11`** — constructs its own `MongoClient`, `GetDatabase`, and `GetCollection<Event>` directly. It never touches `IRepository<T>`.
2. **`Profession/Extensions/ServiceCollectionExtensions.cs:32-35`** — `mongoDatabase.GetCollection<ProfessionEntity>(...)`, then `Find(_ => true).AnyAsync()` and `InsertManyAsync` directly.
3. **All seven `MongoDbConfiguration.cs`** — legitimately construct the `MongoClient`, but each service's `ServiceCollectionExtension` also calls `client.GetDatabase(...)` inline rather than going through an abstraction.

Additionally, `Provider/Provider.csproj:17` references **`MongoDB.Entities` 23.1.0** — a full alternative ODM — and **nothing in the codebase uses it**. Dead dependency on a data-access library that would itself bypass the repository pattern.
