---
feature: api-refactor-pilot-booking
topic: design-roundtable
date: 2026-08-26
mode: subagents
participants: Neo, Bolt, Echo
---

# Meeting Minutes: Design Roundtable
## Feature: api-refactor-pilot-booking | 2026-08-26

**Mode:** Subagents
**Participants:** Neo (Architect), Bolt (Backend Engineer), Echo (QA Engineer)

---

## Context

F-019-T01 — "Spike: DataResponse<T> serialization alongside ObjectIdJsonConverter" — is the first task in F-019's build loop to touch the new `DataResponse<T>` type (ADR-049, defined in `ARCHITECTURE.md` §3). The task boots one route through a throwaway `DataResponse<AppointmentResponse>` wrapper and confirms `ObjectIdJsonConverter` still fires on the nested ObjectId-backed field through the wrapper, before T03 depends on the shape being real. Auto-suggested for a roundtable as the first task to touch new architecture.

---

## Discussion

### Round 1

**Neo (Architect):**
Proposed two approaches: (1) a unit serialization probe round-tripping `DataResponse<AppointmentResponse>` through `JsonSerializer` using production-matching options, or (2) a live throwaway route booted in `Booking/Program.cs` and hit over HTTP. Recommended **Approach 2** — the task title says "boot one route" for a reason, and `ObjectIdJsonConverter` bugs have historically been pipeline-configuration bugs (registration order, attribute placement), which only a real ASP.NET Core pipeline can definitively rule out. Argued a hand-built unit test options object could drift from production wiring and give a false pass.

**Bolt (Backend Engineer):**
Recommended a unit test instead. Confirmed by reading the actual code that `ObjectIdJsonConverter` is registered globally via `options.SerializerOptions.Converters.Add(...)` in `Booking/Program.cs:33-34` (`ConfigureHttpJsonOptions`) — not via `[JsonConverter]` attribute — so there is no attribute-placement risk to catch, and a route proves nothing a unit test against the same `JsonSerializerOptions` wouldn't. Named the real trap: whichever approach is used, if the test manually builds a fresh, non-DI `JsonSerializerOptions` instead of resolving the real one, it silently gives a false negative/positive regardless of route-vs-unit-test.

**Echo (QA Engineer):**
Also favored the unit test — isolates the one variable that matters (converter resolution) without routing/binding/DI noise. Pushed further: this should **not** stay throwaway. "Converter fires through a nested generic wrapper" is a regression-prone serialization contract, not a one-off spike finding — proposed promoting it to a permanent test covering the `Ok` case, the `Fail`/null-`Data` case, a collection-of-ObjectId-items case, and converter ordering.

### Round 2 — Cross-talk

**Neo (responding to Bolt and Echo):**
Converged. Bolt's confirmation that registration is global (not attribute-based) removed the one risk Neo's live-route recommendation depended on — `ConfigureHttpJsonOptions` populates the same `JsonSerializerOptions` a unit test can construct, so a route buys nothing extra. Final call: **unit test**, with Bolt's DI-real-options trap made an explicit acceptance criterion (must resolve the actual DI-configured options, not a hand-rolled instance). On permanence: agreed with Echo — the `AppointmentResponse`/route scaffolding used to explore the shape stays throwaway and gets deleted, but the converter-through-wrapper contract test is promoted to permanent, living alongside `ObjectIdJsonConverter`'s existing coverage.

*Outcome: consensus reached in Round 2 — no Round 3 needed.*

---

## Conclusion

Unit test approach, unanimous after cross-talk: resolve the real DI-configured `JsonSerializerOptions` and round-trip `DataResponse<AppointmentResponse>` through `Serialize`/`Deserialize`, asserting the ObjectId-backed field serializes as a plain string through the wrapper. The specific spike scaffolding is throwaway; the resulting contract test (Ok, Fail/null, collection, converter-ordering cases) is promoted to permanent coverage next to `ObjectIdJsonConverter`'s existing tests.

---

## Next Steps

| # | Action | Owner | Notes |
|---|--------|-------|-------|
| 1 | Write the TDD-gated test resolving real DI-configured `JsonSerializerOptions` (not a hand-rolled instance) — Bolt's named false-negative trap | Build loop | Explicit acceptance criterion per Neo |
| 2 | Cover Ok, Fail/null-`Data`, collection-of-ObjectId-items, and converter-ordering cases | Build loop | Per Echo |
| 3 | Promote the test to permanent, alongside `ObjectIdJsonConverter`'s existing test coverage; delete throwaway scaffolding | Build loop | Per Neo/Echo agreement |
