# Architecture — API Refactor Rollout (F-020)

## 0. Two bundled workstreams — read this before §1

This feature does two independent things, both added to F-020 rather than split into a separate feature
(user direction, mid-Design 2026-08-27):

**(A) Clean Architecture split** — 5 services (Calendar, Customer, Provider, Services, Profession) get
Booking's 4-project shape. §1–§9 below describe this.

**(B) Solution-wide `AgendaBuddy.` rename** — all 30 projects in the solution get the prefix (25 currently
lack it; 5 already have it). This is **broader than (A)**: it also touches `Booking.*` (already shipped),
`Library`, `Library.ServerAuth`, `EventAndCommands`, `Kafka`, `Gateway`, `Identity`, and `MobileApp` (plus
every `.Tests` sibling) — 20 projects that get a pure rename with zero behavioral change, on top of the 5
that get renamed *and* split. §10 describes this workstream on its own.

These interleave for the 5 in-scope services (they're created with the new prefixed name from birth, per
PRD Requirement 19) but are otherwise independent — the 20 pure-rename projects can be sequenced and
verified without any dependency on (A)'s progress, and vice versa.

## 1. Where the Clean Architecture split lives

The same 4-project shape Booking got in F-019, repeated for each of the 5 in-scope services
(`<Service>` = Calendar, Customer, Provider, Services, Profession), created with the `AgendaBuddy.` prefix
from birth (§10):

```
AgendaBuddy.<Service>.Api/            (was <Service> — thin: endpoint definitions, DI wiring, no logic)
AgendaBuddy.<Service>.Core/           (new — MediatR command/query handlers, moved from EventAndCommands/Commands/<Service>)
AgendaBuddy.<Service>.Domain/         (new — commands, queries, request DTOs, its own DataResponse<T>)
AgendaBuddy.<Service>.Infrastructure/ (new — thin/possibly empty, same as Booking.Infrastructure — see §6)
```

`AgendaBuddy.<Service>.Tests` stays one project per service (not split per new project), same rationale as
Booking's. Internal namespaces inside `AgendaBuddy.<Service>.Api` are `AgendaBuddy.<Service>.*` throughout
(e.g. `AgendaBuddy.Calendar.Configuration`, `AgendaBuddy.Calendar.Requests`) — **this is a deliberate
departure from Booking's own precedent**, where `Booking.Api`'s internal namespaces stayed `Booking.*`
rather than becoming `Booking.Api.*` (F-019's `verification.md` §4). That precedent doesn't apply here: it
was about avoiding a *second*, purely cosmetic rename inside a project already being restructured. Here,
the `AgendaBuddy.` prefix on the namespace is Requirement 18 itself, not an optional cosmetic follow-on — so
`AgendaBuddy.<Service>.Api`'s internal `Configuration`/`Requests`/`Validation` sub-namespaces become
`AgendaBuddy.<Service>.Configuration`, `AgendaBuddy.<Service>.Requests`, etc. (dropping `.Api` from the
sub-namespace, matching the pattern Booking already uses for its own sub-namespaces today, just prefixed).

**Notation note for §2–§9 below:** written before the rename requirement was added, using the shorter
`<Service>.Api`/`<Service>.Domain`/etc. form throughout for readability. Every such reference means
`AgendaBuddy.<Service>.Api`/`AgendaBuddy.<Service>.Domain` per §1 above — read the prefix as implied, not
omitted. §10 covers the rename itself, including the 20 projects that have no Clean Architecture split at
all (`Library`, `Gateway`, `MobileApp`, etc.) and are therefore never written as `<Service>.X` below.

## 2. What moves, what stays — per service

| Service | Routes | Handler files moving | Extra shape notes |
|---|---|---|---|
| Calendar | 2 | 2 | Simplest migration — no Kafka, no extra request types. |
| Profession | 2 | 2 | Simplest migration alongside Calendar. |
| Services | 2 | 4 | Route count is low but handler count is higher than routes — some handlers are already finer-grained than their route (worth confirming during Build whether all 4 are actually reachable). |
| Provider | 6 | 6 | Touches Kafka (topic creation on registration) — already has its `IKafkaClient` downcast fixed (`agenda-buddy-5og`, F-018), so no repeat of that specific bug here. |
| Customer | 10 | 4 (+`MessageRequest.cs`, a request type with no matching handler file — investigate at Build whether it's dead or wired differently) | **Largest migration.** Touches Kafka via messaging — and **still carries the dormant `(kafkaClient as KafkaClient)!` downcast bug** `agenda-buddy-5og` was filed against (F-018 fixed Provider's copy only; Booking's was fixed by F-019; Customer's was never touched). This feature fixes it as a byproduct of retyping the handler off the concrete `RequestCollection` shape (PRD Requirement 4/6) — not a separate task, but must not be missed. |

For every service: `<Service>/Requests/RequestCollection.cs`/`IRequestCollection.cs` — **deleted**, replaced
by MediatR dispatch, no replacement type needed (same as Booking). `Library.Services.<Service>Service` —
**unchanged, stays in `Library`.** `Library.Repositories.MongoDbRepository<T>`, `IRepository<T>` —
**unchanged.**

## 3. `DataResponse<T>`: per-service, not shared — decision made now, not deferred again

Booking's own `ARCHITECTURE.md` deferred this exact question to F-020 ("moving it to a shared location is
explicitly F-020's decision, made once the second consumer exists"). The second (through sixth) consumer
now exists. **Decision: keep it per-service**, one `DataResponse<T>` record in each `<Service>.Domain`,
byte-identical in shape to Booking's:

```csharp
namespace <Service>.Domain.Responses;

public sealed record DataResponse<T>(T? Data, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
    public static DataResponse<T> Ok(T data) => new(data, []);
    public static DataResponse<T> Fail(IEnumerable<string> errors) => new(default, errors.ToList());
}
```

**Why not extract a shared type**, now that there are 6 near-identical copies: each service's `.Domain`
project already has to exist regardless (for its own commands/queries), so the envelope sits there at zero
marginal project-reference cost. No code in this repo needs the *same* `DataResponse<T>` type across
service boundaries — the Gateway passes bodies through byte-for-byte without deserializing into a typed
envelope (ADR/threat T-302: no business logic, no path rewriting), and `MobileApp` isn't re-wired by this
feature (§8). A shared project would introduce a new inter-project reference graph across all 6 services
for a single 5-line record — the wrong trade for what's actually needed. If a real cross-service consumer
of the *type itself* (not just the shape) ever appears, that is the trigger to revisit — not "six copies
exist."

## 4. Command/query dispatch flow (same shape as Booking's, repeated per service)

```
<HTTP verb> /api/v1/<service>/<route>
  → <Service>.Api: validate route params, build <Command/Query> from the request
  → mediator.Send(command, ct)          [ct = the real HTTP request's CancellationToken]
  → <Service>.Core.<X>Handler.Handle(command, ct)
      → GuardClauses: null/argument checks on the command
      → Library.Services.<Service>Service.<Method>(...)   [unchanged]
      → EventStore.SaveAsync(event)                        [unchanged — CONSTITUTION §3]
      → returns FluentResults.Result<T>
  → <Service>.Api: map Result → DataResponse<T> (Ok or Fail), map to the right HTTP status via TypedResults
```

**Constructor typing (PRD Requirement 6):** each handler's constructor is typed against the existing
`Library.Services.I<Service>Service` interface, not the concrete class, whenever that interface already
covers everything the handler calls — verified per-handler during Build, not assumed uniformly. Where a
genuine gap exists (a Booking-shaped exception like `AppendAppointmentAsync`), stay on the concrete class
and disclose it in that service's own task/verification notes, same as Booking's `BookAppointmentCommandHandler`
did. **Any handler retyped to an interface must have its DI registration checked against the actual
`ServiceCollectionExtension.cs` for that service** — Booking's own Party Review found this exact retyping
move silently breaks DI if the interface isn't also registered (forwarding to the already-scoped concrete
instance, not a second `AddScoped`), caught only by a full integration-suite run, not a green build.

**Where `Validot` fits, if used for a given route:** validates the incoming request in `<Service>.Api`
before the command is built — same place `MiniValidator.TryValidate` runs today. Per PRD Requirement 9,
this is decided per route, not blanket-applied; a route staying on `MiniValidator` is a disclosed choice,
not a silent gap.

## 5. Integration with existing modules

- **`IMediator` is already registered and injected in all 7 services** — no new DI registration for
  MediatR itself in any of the 5 services, only for each service's moved handlers (registered from that
  service's own `<Service>.Api/Program.cs`, mirroring Booking's `AddMediatR(cfg =>
  cfg.RegisterServicesFromAssemblies(...))` fix for cross-assembly handler scanning).
- **`Library.ServerAuth.AgendaBuddyExceptionHandler`** (F-016) — unchanged, untouched. All 5 services
  already register it; this feature doesn't change that wiring, only what sits above it.
- **`EventStore`/`IEventStore`** — unchanged. Handlers in each `<Service>.Core` call
  `eventStore.SaveAsync(...)` exactly where the current handlers do. **`EventStoreWriteGuardTest`'s
  `ScanRoots` must gain each of the 5 services' new `<Service>.Core` directory** — same fix pattern as
  F-019-T03's `Booking.Core` addition, sized as its own task per service rather than an afterthought (Party
  Review already flagged forgetting this once, at F-018).
- **`IKafkaClient`** — only Customer and Provider touch it. Provider's downcast bug is already fixed
  (F-018, `agenda-buddy-5og`). Customer's is not — this feature fixes it as part of retyping Customer's
  moved handler off the concrete `RequestCollection` shape (§2 table).
- **`AgendaBuddy.IntegrationTests`** — each service's existing anchor alias
  (`CalendarAnchor`/`CustomerAnchor`/`ProviderAnchor`/`ServicesAnchor`/`ProfessionAnchor`, all
  `<Service>.Configurations.MongoDbConfiguration` — note the plural `Configurations`, unlike Booking's
  singular `Configuration`, already correctly aliased in `GlobalUsings.cs`) continues to resolve to each
  service's `<Service>.Api` project after the rename. No harness change needed beyond what the existing
  Contract/Persistence/Audit tests already require to keep passing.

## 6. Architectural decisions

1. **`<Service>.Infrastructure` is thin, possibly near-empty, per service** — same YAGNI rationale as
   Booking's. Do not pad it to look complete; if a service genuinely needs its own infrastructure concern
   during Build, it goes there, and that's disclosed as a real finding, not assumed upfront.
2. **`<Service>.Tests` stays one project per service.** Same rationale as Booking's — no split-per-project
   ceremony without a real consumer needing it.
3. **`DataResponse<T>` stays per-service, not extracted to a shared project** — decided in §3 above, not
   deferred again.
4. **Migration order: Calendar and Profession first (2 routes, 2 handlers each, no Kafka, no extra request
   types) — the lowest-risk pair — then Services (2 routes but 4 handlers, worth confirming reachability),
   then Provider (6 routes, already Kafka-clean), then Customer last (10 routes, the dormant Kafka
   downcast fix, the unexplained `MessageRequest.cs`).** This is the opposite of "hardest first" — Booking
   itself was the single hardest case in the whole program and F-019 already absorbed that risk. Sequencing
   the remaining risk from lowest to highest lets any newly-discovered defect (of the kind F-019 found
   repeatedly) get caught and fixed once, cheaply, before the largest and most Kafka-entangled service.
5. **Route/verb/payload shape does not change for any of the 5 services** — same paths, same HTTP verbs,
   same request bodies. Only the response envelope and internal dispatch mechanism change. Each service's
   existing `<Service>RouteContractTest.cs` (status codes only) keeps passing unmodified.

## 7. Conformance with CONSTITUTION §3

- **MediatR as CQRS dispatcher**: now literally true for all 5 services (was previously registered-but-unused, same as Booking's pre-F-019 state).
- **EventStore audit on every command**: unchanged mechanism, unchanged call-site semantics, moved files.
- **Cache-aside**: unaffected — none of the 5 services' migration touches any existing `CacheAside.GetOrCreateAsync` read site; confirm per-service at Build whether one exists and stays untouched, rather than assuming none do (Booking had none; some of these 5 may).

## 8. What the Clean Architecture split (workstream A) deliberately does not do

- Does not give Booking a second rename beyond the prefix (§10) or touch its architecture again (already done, F-019).
- Does not give Identity a Clean Architecture split (excluded, Discover 2026-08-27 — see the PRD's Out of Scope) — but Identity IS renamed (§10, workstream B).
- Does not re-wire `MobileApp` to any new envelope shape (its own rename in §10 is name-only).
- Does not change any route's path, verb, or request body shape.
- Does not introduce Mapster-based request/response DTOs (PRD Out of Scope — Booking's own attempt at this shipped with zero call sites).
- Does not introduce a shared `DataResponse<T>` package or any other new shared project (§3).
- Does not change any `Library.Services.*` public surface — this is a controller/handler-layer rewrite, not a business-logic rewrite, for all 5 services.

## 9. Open items carried into Plan (workstream A)

- Confirm Services's 4-handlers-for-2-routes shape — is one handler unreachable/dead, or does one route dispatch two different commands depending on some branch? Check before sizing Services's task list.
- Confirm what `Customer/Requests/MessageRequest.cs` actually wires to today — a handler file, an inline check, or nothing (dead code)? Needed before Customer's migration task can be sized accurately.
- Confirm per-service whether any `CacheAside.GetOrCreateAsync` read site exists that must survive the migration untouched (§7) — do not assume "none, like Booking" without checking each service.
- Size Customer's task list larger than the other four (PRD Known Risks) — both for its route count and for the dormant Kafka downcast fix it alone still needs.
- **`EventAndCommands/Commands/` and `/Queries/` will be completely empty of handler implementations once this feature ships** — checked directly: today they contain exactly the 5 in-scope services' folders (Calendar, Provider, Profession, Services, Customer) and nothing else (Booking's moved out in F-019; Identity never had any). After F-020, `EventAndCommands` is purely the EventStore/audit kernel (`Persistence/`, `Events/`, `ConfigurationLoader.cs`) with zero command/query implementations left anywhere inside it. CLAUDE.md's current description ("CQRS kernel: all commands, queries, handlers, events, and EventStore persistence") becomes stale the moment this ships — flagged for the Ship-gate CLAUDE.md refresh, same as every prior feature's doc-staleness pass, not silently left wrong.

## 10. The solution-wide rename (workstream B)

**Scope: all 30 projects.** 5 already correct (`AgendaBuddy.AppHost`, `AgendaBuddy.AppHost.Tests`,
`AgendaBuddy.IntegrationTests`, `AgendaBuddy.ServiceDefaults`, `AgendaBuddy.ServiceDefaults.Tests`). 25 need
the prefix — 5 of those (Calendar/Customer/Provider/Services/Profession) are covered by workstream A's own
project creation (§1). The other **20** are a pure rename, verified inventory (`agenda-buddy.sln`, checked
directly, not estimated):

| Old name(s) | New name(s) | Notes |
|---|---|---|
| `Library`, `Library.ServerAuth`, `Library.Tests` | `AgendaBuddy.Library`, `AgendaBuddy.Library.ServerAuth`, `AgendaBuddy.Library.Tests` | **Do this one first, in isolation.** Everything else references `Library.Entities`/`Library.Services` via `using`/global usings — every other rename (and every service that hasn't even started its own migration yet) needs `Library` renamed and rebuilding clean before anything downstream can be touched safely. |
| `EventAndCommands`, `EventsAndCommands.Tests` | `AgendaBuddy.EventAndCommands`, `AgendaBuddy.EventsAndCommands.Tests` | The `Event`/`Events` inconsistency between these two is preserved, not fixed (PRD Requirement 21/Out of Scope). |
| `Kafka`, `Kafka.Tests` | `AgendaBuddy.Kafka`, `AgendaBuddy.Kafka.Tests` | |
| `Gateway` | `AgendaBuddy.Gateway` | Check `Gateway/AspireServiceDiscoveryProxyConfigProvider.cs` and `AgendaBuddy.AppHost`'s reference/`WithReference` calls — this is the exact class of file F-019-T04's rename cascaded into. |
| `Identity`, `Identity.Tests` | `AgendaBuddy.Identity`, `AgendaBuddy.Identity.Tests` | Rename only — no CQRS split (PRD Out of Scope). |
| `MobileApp`, `MobileApp.Tests` | `AgendaBuddy.MobileApp`, `AgendaBuddy.MobileApp.Tests` | **Highest-risk single rename** (PRD Known Risks) — has its own Android/iOS TFMs, `scripts/run-ios.sh`, is excluded from `agenda-buddy-backend.slnf` but not `agenda-buddy.sln`. Verify with a full-solution build AND the 3 dedicated mobile CI jobs, not the slnf alone. |
| `Booking.Api`, `Booking.Core`, `Booking.Domain`, `Booking.Infrastructure`, `Booking.Tests` | `AgendaBuddy.Booking.Api`, `AgendaBuddy.Booking.Core`, `AgendaBuddy.Booking.Domain`, `AgendaBuddy.Booking.Infrastructure`, `AgendaBuddy.Booking.Tests` | Already-shipped (F-019, `v0.8.0`) — this is a second touch on a feature that just merged. Re-run Booking's own full test suite (516+310 count baseline) after, not just a build check, since this is real churn on recently-shipped code. |

**Cascade points to check per renamed project** (same discipline F-019-T04 applied once, now for 20):
`.sln`/`.slnf` entries, every other project's `ProjectReference`, `AgendaBuddy.AppHost`'s project
references and the Aspire-generated `Projects.<Name>` types it consumes, `AgendaBuddy.AppHost.Tests`'
structural guards (`DockerAndComposeHygieneTest.cs`, `PublishContainerTest.cs`,
`SecurityScanAndDockerJobShapeTest.cs` — anything enumerating service names), every `GlobalUsings.cs` anchor
alias in `AgendaBuddy.IntegrationTests`, `.github/workflows/dotnet.yml`'s Docker matrix + path filters,
`scripts/generate-openapi.sh`/`scripts/run-ios.sh`'s service arrays, `docs/api/openapi/*.json` (regenerate,
don't hand-edit), and every Dockerfile that still exists for the legacy Compose path (`Booking.Api/Dockerfile`,
and whichever of the other 5 still have one — F-017 already deleted `Library`/`Kafka`/`EventAndCommands`'s
broken ones, so this list is shorter than it would have been pre-F-017).

**Sequencing:** `Library` first and alone (everything depends on it). Then the remaining rename-only
projects in any order — they have no inter-dependencies among themselves worth sequencing around, except
that `EventAndCommands` should follow `Library` closely since handlers still living there (post-F-020,
only the ones for services NOT in this feature, plus whatever Identity/EventAndCommands cross-references
exist) reference `Library` types. `Booking.*`'s rename can happen any time after `Library`'s, independent
of workstream A's 5-service migration. `MobileApp`'s rename, given its risk profile, is a reasonable
candidate for a dedicated task with its own explicit full-suite + mobile-CI verification, not bundled
into a batch with lower-risk renames.
