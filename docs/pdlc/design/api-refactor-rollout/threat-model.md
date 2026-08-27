# Threat Model — API Refactor Rollout (F-020)

**Triage:** Lite (Phantom solo) — same triage outcome as Booking's (F-019), for the same reason: no new
routes, no new trust boundary, no new external integration, repeated 5 times.

**Trust boundary changes:** No. Same routes, same auth middleware, same `AgendaBuddyExceptionHandler`, for
all 5 services.
**Regulated/PII data:** No new exposure — same fields, same entities, same persistence, for all 5 services.
**New attack surface:** No new routes, no new external integration, no new dependency with network access.
The two real risks are the same class F-019 already found and fixed once: validation-strictness regression
during a library swap, and response-envelope error leakage — both risks repeat per service, not per route,
so they are modeled once here rather than five times.

## Threats considered

### T-201 — Validot (where adopted per service) validates less strictly than the current check did (mitigate now)

**Scenario:** Same as Booking's T-101. Wherever a service's task list chooses to migrate a route from
`MiniValidator.TryValidate` (or an inline check, for Calendar/Profession, which have zero `MiniValidator`
calls today) to Validot, a hand-written rule that's looser than the original could let an invalid request
reach the handler where it previously 400'd. F-019's own Party Review found exactly this class of bug live
— `.Required().NotEmpty()` accepting whitespace where `.NotWhiteSpace()` was needed — caught before shipping
by probing the real Validot assembly, not assumed from the rule's name.

**Mitigation:** For every route a service's task migrates to Validot, a test asserts the exact same request
that 400s today still 400s after the port — reusing that service's existing `<Service>RouteContractTest.cs`
status-code assertions as the regression check (PRD AC 7 already covers this structurally). Before wiring
any new/moved Validot spec into a route, probe it live against the real Validot assembly for the specific
edge case being replaced (empty string, whitespace-only, null) — do not assume from the rule's name.

### T-202 — `DataResponse<T>`'s `Errors` field leaks internal detail (mitigate now)

**Scenario:** Same as Booking's T-102, repeated per service — a `FluentResults.Result.Errors` collection,
if serialized verbatim, could include exception messages, stack fragments, or internal type names not
meant for a client, for any of the 5 services' routes.

**Mitigation:** Same as Booking's — each service's `DataResponse<T>.Fail` takes `IEnumerable<string>`; the
mapping from `FluentResults.Result` to `DataResponse<T>` must extract only the intended human-readable
message, never `ToString()` an exception. Enforce with at least one fault-injection test per service
forcing a genuine unhandled exception (not mocked) and asserting the wire response carries no exception
detail — same pattern as Booking's `BookingErrorLeakageTest`, plus (per F-019's own Party Review finding)
at least one test forcing a genuine *handled* `Result.Fail` through to the wire, not just the unhandled
path.

### T-203 — `EventStoreWriteGuardTest` silently stops covering a service after its handler files move (accept, tracked)

**Scenario:** Same as Booking's T-103 — the guard's file-path enumeration doesn't auto-follow a project
move. Repeated per service: each of the 5 services' `<Service>.Core` needs its own `ScanRoots` addition.

**Disposition:** Not a "mitigate now" security control — a coverage-maintenance task, sized explicitly per
service at Plan (`ARCHITECTURE.md` §5/§9), same as Booking's F-019-T03.

### T-204 — Customer's dormant `IKafkaClient` downcast, if migrated without retyping (accept — fixed as a byproduct, tracked so it isn't missed)

**Scenario:** Customer's `AddCustomerCommandHandler` constructor is typed to the concrete `KafkaClient`
class, currently only safe because `RequestCollection` hand-casts `(kafkaClient as KafkaClient)!` from the
`IKafkaClient` DI registration that happens, today, to back onto the concrete type. This is the exact
architectural shape `agenda-buddy-5og` was filed against — Provider's copy was fixed at F-018, Booking's at
F-019; Customer's was never touched. If this feature moves the handler to real `mediator.Send` dispatch
without also retyping its constructor to `IKafkaClient?`, DI resolution throws
`InvalidOperationException: Unable to resolve service for type 'KafkaClient'` the instant a real dispatch
hits it — not a security vulnerability, but a guaranteed runtime failure this feature must not reintroduce
under a different symptom.

**Disposition:** Fixed as a natural consequence of PRD Requirement 4 (delete `RequestCollection`, dispatch
via MediatR) applied to Customer — not a separate task, but explicitly named here so it isn't silently
skipped the way Customer's copy already was once.

## Threats not applicable

- **Injection** — no new query construction; every `Library.Services.*`/`MongoDbRepository<T>` call site is
  unchanged for all 5 services.
- **IDOR** — `OwnershipGuard`/`AssertRole` calls unchanged in location and logic, for all 5 services.
- **Secrets** — no new credential, key, or config surface.
- **Supply chain** — `FluentResults`, `Validot`, `GuardClauses` are already ADR-049-approved and in
  production use via Booking; no new package. (Mapster is also approved but this feature does not adopt it
  — PRD Out of Scope.)
