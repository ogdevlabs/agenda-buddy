# Blast Radius — secure-public-endpoints (F-016)

**Scope:** public + signature-changed symbols. The diff is **158 files / +9581 / −481**, which is well past the
~40-file threshold, so per the scope table this is *not* every changed symbol — it is exported/public
symbols plus anything whose signature, return type, or error contract changed.

**Symbols examined:** 19 · **Call sites found:** 41 · **⚠ At risk:** 0 · **Untested changed paths:** 1

**Searches run:** `grep -rn --include="*.cs"` across the whole repo, excluding `/obj/` and `/bin/`, for each
symbol below; plus route-string and HTTP-call greps across `MobileApp/`.

> **Evidence only.** This step locates suspicions; the reviewers verify them and own the findings. No
> severity labels here.

---

## ⚠ At risk

**None found.** Stated as a search result, not a safety claim — see *Limitations* at the bottom for what a
grep cannot see.

The two candidates that looked at-risk on inspection, and why each resolved:

| Symbol | Change | Caller outside the diff | Still valid? |
|---|---|---|---|
| `OwnershipGuard.AssertOwner` | param `string` → `string?`; now throws when **either** side is null | `Provider.Tests/Auth/ProviderOnboardingAuthTest.cs:15,23,30`, `Customer.Tests/Auth/CustomerOnboardingAuthTest.cs:13,21` | **Yes.** Source-compatible for non-null arguments, and every one of these passes a non-null literal. Checked specifically for a pre-existing test asserting the *old* null-passes behaviour — there is none (the `Assert.Null(ex)` hits are `Record.Exception` results, not null-claim cases). |
| `IRepository<T>` | gained `GetPagedAsync` | `Identity.Tests/Helpers/InMemoryRepository.cs` | **Yes** — updated in the diff. Adding a member to an interface breaks every implementer, so this was the one that had to be enumerated exhaustively: `grep ": IRepository<"` returns exactly **two** implementers, both updated. |

---

## Contract changes

| Contract | Consumers named | Verdict |
|---|---|---|
| **5 GET routes now require auth** (`/providers`, `/providers/{email}`, `/customers`, `/customers/{email}`, `/services/{email}`) | **zero in-repo callers** | See *route-consumer sweep* below — this is the load-bearing justification for the whole exposure-closure wave, so it was checked directly rather than taken from the PRD. |
| **`GET /providers` + `GET /customers` response shape** — bare array → `{items,totalCount,page,pageSize}`, and `204` retired | zero in-repo callers | Same sweep. **F-015 will be written against the new shape**, so the corrected `api-contracts.md` §4/§5.1 is the hand-off artifact. |
| **`GET /providers*` payload** — non-owners now receive `ProviderSummary`, not `ProviderEntity` | zero in-repo callers | Fields dropped: `appointmentEntities`, `subscribedCustomerCollection`, `kafkaTopic`, `_id`. |
| **`POST /api/v1/professions` deleted** | zero in-repo callers | Its write path (`EventsHelper.AddProfessionEvent`, `RequestCollection.AddProfessionRequest`) was deleted with it; `AddProfessionCommand`/`Handler` deliberately retained. |
| `Provider`/`Customer` `IRequestCollection` — `GetProvidersRequest` / `GetCustomersRequest` signature **and** return type | 1 implementer each (updated) + `Customer.Tests/Events/EventsHelperTest.cs:76` | **Updated.** The Moq `Setup` had to change too — Moq binds to the old signature at compile time, so this surfaced as a build error rather than a silent pass. |
| `GetProvidersQuery` / `GetCustomersQuery` — `IRequest<List<T>>` → `IRequest<PagedResponse<T>>` | handlers only (updated) | MediatR never dispatches these (`15-cqrs-and-messaging.md:16-57`); `RequestCollection` hand-constructs the handlers, so there is no registry to fall out of sync. |
| `EventStore` ctor — gained `IHttpContextAccessor` | DI only — `grep "new EventStore("` returns **zero** direct constructions | **Compatible.** `AddEventStore()` registers `AddHttpContextAccessor()` itself, so no consumer had to change. |
| `Event` — gained nullable `Actor` | 12 handlers write `Event`; nothing reads `Actor` outside tests | **Compatible.** Additive, and MongoDB is schemaless, so existing documents deserialize with `Actor = null`. |
| `AgendaBuddyExceptionHandler` registered in 6 services — `ForbiddenException` now 403 where it was 500 | every route in those 6 services | Widening, not narrowing: the handler returns `false` for all other exception types, so previously-500 paths other than `ForbiddenException` are untouched. |

### Route-consumer sweep — the one that mattered

The PRD's central safety argument for five breaking changes is *"zero reachable consumers"*, sourced to
`01-api-surface.md:158` (mobile paths omit `api/v1/`). Verified independently, and the result is **stronger
than the PRD claims**:

```
MobileApp route strings found: "booking/{id}", "booking?date=", "calendar?from=",
                               "customer", "notifications", "notifications/{id}/read",
                               "messages", "messages/thread/{id}",
                               "api/v1/auth/login", "api/v1/auth/register"
```

- `CustomerApiService` calls **`"customer"` (singular)** — not `api/v1/customers`. Different route.
- `CalendarApiService` calls **`"calendar?from=…"`** — not `api/v1/calendar/appointments/{email}`.
- **There is no provider API service at all** (`MobileApp/Services/` has no `ProviderApiService`), so the
  headline route has no client-side caller even by the wrong name.
- The only correctly-pathed calls are `api/v1/auth/login` / `register`, which **F-016 does not touch**.

So the five changed routes have **no in-repo consumer at all** — not merely an unreachable one. The PRD's
assumption holds, by a wider margin than it claimed.

---

## Untested changed paths

| Symbol | Test found? |
|---|---|
| `MongoDbRepository<T>.GetPagedAsync` | **No direct test.** Not unit-testable: both ctors need a live database and the driver's fluent chain ends in an extension method Moq cannot intercept. Exercised end-to-end by `PaginationTest`'s 9 cases against a real container. Feeds Echo's coverage verdict — recorded, not adjudicated. |

Every other changed symbol has at least one test exercising the changed path. `AssertOwnerAny` and
`AssertRole` were **read but not modified** by this feature, so they are out of scope here; note that
`AssertRole` gained its **first two call sites ever** (`Customer/Program.cs`, `Provider/Program.cs`), which is
a behaviour change for the `role` claim even though the guard's own code is untouched.

---

## Full call-site map

| Symbol | Call sites (classification) |
|---|---|
| `IRepository<T>.GetPagedAsync` | `MongoDbRepository.cs:89` (updated) · `InMemoryRepository.cs:68` (updated) · `ProviderService.cs:19`, `CustomerService.cs:16` (added) |
| `OwnershipGuard.AssertOwner` | `Calendar:149,195` (added) · `Provider:152` (added), `Provider:273` (unchanged — retains its local catch per AC-14) · `Customer:171` (unchanged; local catch removed for AC-13) · `Services:153,177` (unchanged) · 5 test call sites (unchanged — compatible) |
| `OwnershipGuard.IsOwner` (new) | `Provider/Program.cs:252` |
| `OwnershipGuard.AssertRole` (unmodified, newly used) | `Customer/Program.cs` (added) · `Provider/Program.cs` (added) — previously **zero** call sites (`13-security.md:137`) |
| `ProviderService.GetPagedProvidersAsync` (new) | `GetProvidersQueryHandler` |
| `CustomerService.GetPagedCustomersAsync` (new) | `GetCustomersQueryHandler` |
| `QueryAudit.Success` / `.Failure` (new) | 18 sites across 9 query handlers (all added) |
| `AuditActor.From` (new) | `EventStore.cs:59` |
| `Event.Actor` (new) | written at `EventStore.cs:59`; read only in tests |
| `PageRequest.Clamp` (new) | `Provider/Program.cs`, `Customer/Program.cs` |
| `PagedResponse<T>.From` (new) | `Provider/Program.cs`, `Customer/Program.cs`, both list query handlers |
| `ProviderSummary.From` (new) | `Provider/Program.cs` × 2 (list + single) |
| `AgendaBuddyExceptionHandler` (new) | registered in 6 `Program.cs` (`AddExceptionHandler<>` + `UseExceptionHandler()`) |
| `EventsHelper.AddProfessionEvent` | **deleted** — sole caller (`Profession/Program.cs` POST route) deleted in the same commit; sole test deleted (see review) |
| `IRequestCollection.AddProfessionRequest` | **deleted** — sole caller was `EventsHelper.AddProfessionEvent` |
| `HostileEndpoints` (new, test-only) | `MongoEndpointGuardTest`, `MongoFailClosedTest` |

---

## Limitations

Named rather than left implicit, because "no callers found" is a statement about the search, not a proof:

- **Reflection / DI / dynamic dispatch hide callers from grep.** `EventStore`, `IRequestCollection`,
  `IRepository<T>` and every `IExceptionHandler` are resolved through the DI container, so the compiler and
  the container — not grep — are the real authority. Mitigation used here: the solution **builds clean with
  0 warnings** and all three suites pass, which exercises the DI graph for all 7 services (the AppHost tests
  additionally construct the app model).
- **Cross-repo consumers are not verifiable from this repo.** There is no evidence of any external client of
  these routes, and the repo is the only known consumer, but that is an absence of evidence.
- **The context catalog is stale by design at review time.** Where it and the code disagreed, the code won —
  and there were **three such disagreements**, all reported in the review: the handler count
  (`15-cqrs-and-messaging.md:161`), the catch-site count, and the `profession`/`duration` fields in
  `api-contracts.md` §5.1.
