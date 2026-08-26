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

F-019-T03 — "Scaffold Booking.Domain + Booking.Core + Booking.Infrastructure; migrate the 3 original routes' commands/handlers" — is the first task to touch F-019's new Clean Architecture projects. It moves `BookAppointmentCommand`/`UpdateAppointmentCommand`/`CancelAppointmentCommand` and their handlers out of `EventAndCommands/Commands/Booking/`, without touching `Booking.Api`/`Program.cs`'s dispatch behavior (T04's job). Auto-suggested for a roundtable as the first task to touch new architecture. A real, verified risk surfaced before the roundtable even started: `AgendaBuddy.IntegrationTests/Audit/EventStoreWriteGuardTest.cs` (a permanent CONSTITUTION §3 guard) walks only `EventAndCommands/` for handler files and asserts `HandlerFiles().Count >= 20` — moving 3 handlers out drops that below 20, failing the guard's own sanity check. The formal fix is task F-019-T07, which depends on T04 **and** T06 — much later than T03.

---

## Discussion

### Round 1

**Neo (Architect):**
Proposed two approaches: (1) Move + Rename Now ("Clean Cut") — relocate and rename the namespace to `Booking.Domain`/`Booking.Core` conventions in the same task; (2) Move, Keep Namespace ("Physical Move Only") — relocate files but keep `namespace EventAndCommands.Commands.Booking;` unchanged, deferring the rename to a follow-up once T04's dispatch rewrite lands. Recommended **Approach 2** — isolating the physical move from the semantic rename means a break can be attributed to one change, not two stacked together. On the `EventStoreWriteGuardTest` gap: **do not defer to T07 silently** — the guard's own sanity check would hard-fail the moment T03 lands, which is a broken CONSTITUTION §3 structural guard, not just reduced coverage. Recommended a minimal defensive fix now: widen the scan to a second root (`Booking.Core/`) and note the change for T07, rather than doing T07's full generalization early.

**Bolt (Backend Engineer):**
Recommended renaming now, having checked the actual files. The reference surface is small and fully enumerable today (6 files' own `namespace` line, one `global using` line in `Booking.Tests/GlobalUsings.cs`, `RequestCollection.cs`'s implicit resolution) — deferring doesn't shrink it, it grows it, since T04/T06 will add new DI registrations and dispatch code against whichever namespace exists at the time. Confirmed a concrete project-reference requirement: `Booking.Core` needs direct references to `Kafka.csproj` and `EventAndCommands.csproj` (handlers take concrete `KafkaClient?`/`IEventStore`), with no circularity risk as long as the graph stays `Api→Core→Domain/Infrastructure` with no back-references. Flagged file-move traps: the file-level `#pragma warning disable CS9113` (no matching restore), `[ExcludeFromCodeCoverage]` attributes, and the ADR/threat-referencing `<remarks>` comments must all survive the move verbatim. Agreed with Neo on the guard: fix it now in T03 with a one-line second scan root — not overreach into T07's real job (exclusion-list/threshold/general-convention logic).

**Echo (QA Engineer):**
Agreed the `EventStoreWriteGuardTest` gap cannot be left for 4+ tasks — the loud half (the `>=20` assertion going red) would get caught, but the silent half (3 handlers quietly falling out of per-handler `[Theory]` coverage) is exactly the regression class this project's culture exists to prevent, and it's invisible to the backend-only gate every normal build loop runs. Pushed back explicitly on "leave broken + file a tracked issue" as a resolution — insufficient on its own. On T03's own test bar: existing `EventsHelperTest.cs`/`AppointmentLifecycleTest.cs` passing unchanged (modulo `using` updates) is the right minimum — that's the proof the move is behavior-preserving; new tests for the moved handlers themselves would be scope creep unless the move exposes a real coverage gap. Flagged that T03 having no PRD-level ACs is itself a process gap — insisted on an explicit minimum bar regardless: `Booking.Tests` green post-move, and a deliberate, surfaced note on whether the guard was patched (not buried in a commit message).

### Round 2 — Cross-talk

**Neo (responding to Bolt):**
Flipped to Approach 1 — rename now. Reassessed the risk-isolation argument: a namespace rename is 100% compiler-enforced, so if it's wrong the build fails immediately at the exact reference — a fundamentally different risk category from the physical move (project references, DI wiring, transitive access), where breakage can be quieter. Stacking a mechanical, compiler-checked rename on the move doesn't compound *risk*, only *diff size* — and Bolt's point stands on diff size: deferring doesn't shrink it, T04/T06 only add to the reference count in the meantime. Final call: move and rename together, in T03, alongside the guard fix already agreed with Echo and Bolt.

*Outcome: consensus reached in Round 2 — no Round 3 needed.*

---

## Conclusion

Move + Rename Now: relocate the 3 commands/handlers into `Booking.Domain/Commands/`/`Booking.Core/Commands/` with the namespace renamed immediately (not deferred), update `RequestCollection.cs` and `Booking.Tests/GlobalUsings.cs` in the same commit, add the required `Booking.Core→Kafka/EventAndCommands` project references, and patch `EventStoreWriteGuardTest.HandlerFiles()` with a second scan root as a minimal defensive fix — all in T03 itself, not deferred to T07.

---

## Next Steps

| # | Action | Owner | Notes |
|---|--------|-------|-------|
| 1 | Move + rename the 3 commands to `Booking.Domain.Commands`, handlers to `Booking.Core.Commands` (folder paths per ARCHITECTURE.md §2) | Build loop | `ChangeAppointmentStatusCommand`/Handler stays untouched — out of scope |
| 2 | Update `RequestCollection.cs` and `Booking.Tests/GlobalUsings.cs` to the new namespaces in the same commit | Build loop | Behavior stays hand-constructed — no `mediator.Send` yet (T04) |
| 3 | Add `Booking.Core` → `Kafka.csproj`/`EventAndCommands.csproj` project references | Build loop | Per Bolt's confirmed constructor-parameter requirement |
| 4 | Patch `EventStoreWriteGuardTest.HandlerFiles()` with a second scan root (`Booking.Core/`) | Build loop | Minimal defensive fix; full generalization stays T07's job |
| 5 | Preserve `#pragma warning disable CS9113`, `[ExcludeFromCodeCoverage]`, and ADR/threat `<remarks>` comments verbatim through the move | Build loop | Per Bolt's flagged traps |
| 6 | Confirm `EventsHelperTest.cs`/`AppointmentLifecycleTest.cs` pass unchanged (modulo `using`) | Build loop | Proof the move is behavior-preserving, per Echo |
