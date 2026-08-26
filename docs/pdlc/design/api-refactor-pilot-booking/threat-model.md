# Threat Model — API Refactor Pilot: Booking (F-019)

**Triage:** Lite (Phantom solo)

**Trust boundary changes:** No. Same routes, same auth middleware, same `AgendaBuddyExceptionHandler`.
**Regulated/PII data:** No new exposure — same fields, same entity, same persistence.
**New attack surface:** No new routes, no new external integration, no new dependency with network access.
The one real risk is internal: swapping the validation library.

## Threats considered

### T-101 — Validot validates less strictly than MiniValidator did (mitigate now)

**Scenario:** `MiniValidator.TryValidate` currently enforces Booking's `[Required]`/`[EmailAddress]`
data-annotation attributes. If `Validot`'s rules are hand-written per DTO (not auto-derived from
annotations) and a rule is dropped or written more loosely during the port, a request that should 400 could
instead reach the handler with invalid data.

**Mitigation:** For every DTO, a test asserts the exact same request that 400s today still 400s after the
port — reusing `BookingRouteContractTest.cs`'s existing status-code assertions as the regression check
(Requirement 13 already covers this structurally; this threat is why that AC exists, not a new control).

### T-102 — `DataResponse<T>`'s `Errors` field leaks internal detail

**Scenario:** A `FluentResults.Result.Errors` collection, if serialized verbatim, could include exception
messages, stack fragments, or internal type names not meant for a client.

**Mitigation:** `DataResponse<T>.Fail` (§3, ARCHITECTURE.md) takes `IEnumerable<string>` — the mapping from
`FluentResults.Result` to `DataResponse<T>` must extract only the intended human-readable message, not
`ToString()` an exception. Enforce with a test asserting no response body contains a stack-trace-shaped
string (matches the spirit of F-016's `AssertRole`/`ForbiddenException` centralization: fail closed on
detail, not open).

### T-103 — `EventStoreWriteGuardTest` silently stops covering Booking after the file move (accept, tracked)

**Scenario:** Already named in ARCHITECTURE.md §5 and the PRD's Known Risks — the guard's file-path
enumeration doesn't auto-follow a project move. Not a new vulnerability class, but a real regression-net gap
if missed.

**Disposition:** Not a "mitigate now" security control — it's a coverage-maintenance task, sized explicitly
at Plan (§9 of ARCHITECTURE.md) rather than left implicit.

## Threats not applicable

- **Injection** — no new query construction; `Library.Services.BookingService`/`MongoDbRepository<T>`
  unchanged.
- **IDOR** — `OwnershipGuard` calls unchanged in location and logic.
- **Secrets** — no new credential, key, or config surface.
- **Supply chain** — `FluentResults`, `Validot`, `Mapster`, `GuardClauses` are already ADR-015-approved
  (as amended by ADR-049); no new package beyond what's already vetted.
