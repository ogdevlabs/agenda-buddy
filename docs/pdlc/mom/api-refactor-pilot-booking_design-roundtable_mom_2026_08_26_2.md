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

F-019-T02 — "Spike: Validot rule API against Booking's existing request DTOs" — replaces `MiniValidator.TryValidate` with `Validot` (ADR-015/ADR-049 approved, already in the NuGet cache but not yet referenced by any `.csproj`). Corrected premise (per the Wave 1 kickoff standup): `AppointmentEntity` carries `[EmailAddress]` only; the 3 F-014 request records carry zero annotations. Auto-suggested for a roundtable as a third-party-integration task. Threat T-101 (Lite threat model, mitigate now) names the risk directly: a hand-written Validot rule dropped or loosened during the port could let an invalid request reach the handler — the real parity test is assigned to a later task (F-019-T08); T02's job is to establish the rule-authoring pattern T05/T06/T08 build against.

---

## Discussion

### Round 1

**Neo (Architect):**
Proposed two approaches: (1) Standalone Harness Spike — Validot `Specification<T>` definitions for all 4 DTOs in an isolated test file, zero blast radius, no `Program.cs` change; (2) Inline Port Spike — swap MiniValidator for Validot on one live route for real. Recommended Approach 1 — T02's job is to hand T05/T06 a proven pattern, not ship a route, and a false start baked into working code is more expensive to unwind than a throwaway harness. Also resolved ARCHITECTURE.md's open question: **replace, don't wrap** — Validot rules become the single source of truth per DTO (including re-deriving `AppointmentEntity`'s `[EmailAddress]`), since dual validation (annotations + Validot both live) is exactly the drift T-101 warns about.

**Bolt (Backend Engineer):**
Validot's API is purely hand-written `Specification<T>` — zero DataAnnotations awareness, a full rewrite of intent, not a mechanical port. Named a concrete, real trap: Validot's `.Required()` treats `null` as invalid but empty string `""` as **valid** by default — matching current `[Required]` semantics needs `.Required().NotEmpty()` chained. Second trap: nullable fields (`PaymentRequest.Currency`) must not get `.Required()` at all, or that itself introduces the exact strictness regression T-101 warns about. Recommendation for the fastest trustworthy answer: write one real `Specification<T>` against `AppointmentEntity` (the messiest case) **including the DI registration and a `Program.cs` call-site swap**, verified against 2-3 requests that 400 today under MiniValidator — the other 3 DTOs are structurally simpler flat records and the pattern transfers trivially, so skip live-wiring them in the spike.

**Echo (QA Engineer):**
Agreed T02 stays exploratory — real parity coverage is T08's job by design; duplicating it now creates two sources of truth that can drift. But required a concrete deliverable regardless of approach: a per-DTO, per-field list of current MiniValidator behavior (confirmed, not recalled) vs. the proposed Validot rule, flagging any field being translated from memory rather than confirmed. Named a landmine in the opposite direction from Bolt's: `AppointmentStatusRequest.Status` has no enum check today and `PaymentRequest.Amount` has no positivity check today — if T02's pattern "helpfully" adds either, that's new stricter behavior, not a port, and fails T08's parity test by over-tightening rather than under-tightening. Both must be explicitly flagged "not ported — new behavior," not silently included.

### Round 2 — Cross-talk

**Neo (responding to Bolt and Echo):**
Converged on a third, synthesized approach — **Vertical Slice Spike**, neither original option. Bolt's finding (the `.Required()`/`.NotEmpty()` trap) is exactly the DI/mapping friction Neo's harness rejection worried about surfacing too late — it would have slipped through a pure harness. Final call: do the real end-to-end swap (DI + `Program.cs` call-site), but scoped to exactly one route (`POST /appointments`, `AppointmentEntity`) — the minimum real wiring needed to retire the coexist-vs-replace and DI/mapping unknowns. The other two Booking routes are explicitly T05/T06's job, not T02's. T02 concretely delivers: (1) the one real vertical slice, verified against 2-3 currently-400ing requests; (2) authored-but-not-wired `Specification<T>` rules for the 3 annotation-less records, applying Bolt's nullable-field lesson; (3) Echo's per-field diff list, with `Status` and `Amount` explicitly flagged "not ported — new behavior, out of scope for T08 parity"; (4) a short written note on the semantic gotchas for T05/T06.

*Outcome: consensus reached in Round 2 — no Round 3 needed.*

---

## Conclusion

Vertical Slice Spike: one real end-to-end DI + `Program.cs` swap on `POST /appointments`/`AppointmentEntity`, plus authored-only `Specification<T>` rules for the 3 F-014 records, plus a mandatory per-field diff list flagging any proposed rule that isn't a straight port (both under- and over-tightening risks named explicitly). "Replace, don't wrap" stands as the architectural decision — Validot becomes the sole validation source of truth per DTO.

---

## Next Steps

| # | Action | Owner | Notes |
|---|--------|-------|-------|
| 1 | Build the real vertical slice: Validot `Specification<AppointmentEntity>`, DI registration, `Program.cs:153` (POST /appointments) call-site swap | Build loop | Verified against 2-3 currently-400ing requests, not memory |
| 2 | Apply `.Required().NotEmpty()` where current `[Required]`-on-string semantics must be matched; never `.Required()` on nullable fields | Build loop | Per Bolt's named trap |
| 3 | Author (not wire) `Specification<T>` for `AppointmentStatusRequest`, `NoteRequest`, `PaymentRequest` | Build loop | Live wiring deferred to T05/T06 |
| 4 | Produce the per-field diff list; flag `AppointmentStatusRequest.Status` (no enum check today) and `PaymentRequest.Amount` (no positivity check today) as "not ported — new behavior" | Build loop | Feeds T08's real parity test directly |
