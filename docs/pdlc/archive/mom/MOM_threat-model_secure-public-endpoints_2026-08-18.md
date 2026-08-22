# MOM — Threat Modeling Party: secure-public-endpoints (F-016)

**Date:** 2026-08-18
**Lead:** Phantom (Security Reviewer) — Neo handed lead off at Step 10.5
**Participants:** Phantom, Neo, Bolt, Echo, Pulse, Atlas, Muse, Jarvis, Friday — 9 agents
**Spawn mode:** `solo`. The session carries a standing "do not call the Agent tool unless requested" instruction, which overrides STATE.md's `Party Mode: agent-teams`. Recorded rather than silently substituted — the same disclosure made for the wave-1 standup.
**Deliverable:** [`threat-model.md`](../design/secure-public-endpoints/threat-model.md)
**Minutes:** Jarvis

---

## Triage

| Question | Answer | Evidence |
|---|---|---|
| Trust boundary changes | **yes** | Creates an authorization boundary at 5 routes that had none; adds ownership scoping to 2; adds the solution's **first two `AssertRole` call sites**; moves exception handling into Production. |
| Regulated data | **yes** | Provider/customer names + emails; appointment records linking a named customer to a named provider. Cluster is synthetic, but the data classes are PII. |
| New attack surface | **yes** | New client-controlled input (`page`, `pageSize`); new response type; a new `IExceptionHandler` running in Production; a **new harness whose connection-string resolution can reach a live cluster**. |

**Outcome: Full (3/3).** Party convened.

---

## Framing

Phantom opened by narrowing the brief:

> **Phantom:** "This is a threat model *of a security fix*. Cataloguing how the old code was insecure is not the job — the PRD already did that, with `file:line` anchors. The job is: **how does the new design fail?** Where can it be bypassed, and what does it make *worse*? If we come back with eight restatements of the PRD's problem statement, we have wasted the party."

That framing shaped the output: **five of the eight threats are introduced or made newly reachable by this feature**, not inherited.

---

## Layer 1 — Surface threats (divergent)

| Threat | Contributing agent(s) | Boundary | STRIDE |
|---|---|---|---|
| T-001 null-claim ownership bypass returns the full appointment book | Neo → **Phantom** | TB-2 | EoP / Info disclosure |
| T-002 harness fail-closed guard bypassable if matched on hostname | Phantom → **Pulse** | TB-5 | Tampering / DoS |
| T-003 any authenticated user still extracts the whole customer table | **Atlas** → Phantom → Bolt | TB-2 | Info disclosure |
| T-004 new Production exception handler as a disclosure channel | Neo → **Phantom** | TB-6 | Info disclosure |
| T-005 audit payload reduced without adding an actor | Phantom → **Echo** → Atlas | TB-4 | Repudiation |
| T-006 future refactor moves the guard behind the cache | Neo → **Echo** | TB-2 | Info disclosure |
| T-007 role-gating professions gates the wrong thing — no admin role exists | **Bolt** → Phantom → Neo | TB-3 | EoP / Tampering |
| T-008 token minted for any purpose authorizes everything | Phantom | TB-1, TB-3 | Spoofing / EoP |

Plus six LOW findings recorded in the threat model's deprioritized table.

---

## Cross-talk highlights — the chains that single-agent review would have missed

**Chain 1 → T-001, the most valuable finding of the party.**
> **Neo:** "§3.3 selects the response shape by comparing the caller's `sub` to the provider's email. Owner gets the full entity, non-owner gets `ProviderSummary`."
> **Phantom:** "Which comparison primitive?"
> **Neo:** "`OwnershipGuard.AssertOwner` — it's the established one, used at eight sites."
> **Phantom:** "That's the one with the null asymmetry. `13-security.md:135`: `string.Equals(null, null)` is `true`, so the guard **passes** on a null claim. `AssertOwnerAny` checks for it; `AssertOwner` doesn't. Right now that hole is unreachable at these routes because nothing there branches on ownership. **Your design is what makes it reachable — and it lands on the owner branch, which returns the unprojected entity.**"
> **Neo:** "Then requirement 18 is misfiled. The PRD deferred that fix to F-021. It has to be fixed here."

Neither document surfaces this alone: ARCHITECTURE treats ownership as a solved primitive, and the PRD had already filed the null bug under a *different feature*. **Party recommendation: reassign PRD requirement 18 from F-021 into F-016.**

**Chain 2 → T-002, where the obvious mitigation was wrong.**
> **Phantom:** "Requirement 5 says fail closed if the connection string isn't a Testcontainer endpoint. Simplest check: assert the host is localhost."
> **Pulse:** "That fails here specifically. Reaching Atlas through `kubectl port-forward` or an SSH tunnel presents as localhost — and a developer may legitimately run their own Mongo there. **Localhost is not a trust signal on this project.**"
> **Phantom:** "Then the guard must assert *identity*, not shape — compare against the endpoint the Testcontainers API reports for the container this fixture just started. Plus refuse anything carrying `mongodb+srv://` or credentials, as a cheap second layer."
> **Neo:** "And it belongs in fixture construction, not a test attribute — abort before any database is created."

**Chain 3 → T-003, reframed by a product question rather than a security one.**
> **Atlas:** "Before we talk controls — who is `GET /api/v1/customers` *for*? F-003 defines discovery as customers finding **providers**. There's no flow where a user lists all customers. The only defensible caller is a provider looking at their own subscribers."
> **Phantom:** "Then authentication alone is nearly worthless there. Registration is anonymous, unverified and unthrottled — an attacker signs up as a `Customer`, gets a token, and pages through the whole table. `totalCount` even tells them how many pages. **Pagination bounds the response; it does not bound extraction.**"
> **Bolt:** "The role check is the identical one-liner as requirement 13. Marginal cost over approved scope is near zero."
> **Muse:** "No UX cost either — no shipped screen consumes that route. The mobile client can't reach it at all."

**Chain 4 → T-006, where documentation was upgraded to a test.**
> **Neo:** "I've recorded guard-before-cache as a design invariant in ARCHITECTURE §8 and the API contract."
> **Echo:** "An invariant guarded only by prose isn't guarded. F-019/F-020 rewrite every one of these files. Make it a test: warm the cache as the owner, then request the same `{email}` as a different principal, assert 403."
> **Bolt:** "Careful with the assertion. `CacheAside` returns `default!` on a 500 ms lock timeout, which surfaces as a spurious 404 — and it has no test at all. Assert 'not 200 with data', not 'exactly 403', or Build chases phantom failures."

**Chain 5 → T-007, found by trying to implement it.**
> **Bolt:** "I went to write the role check for requirement 13 and there's no role to check for. The allow-list is `{Provider, Customer}` — `Identity/Program.cs:100-106`. **No admin.**"
> **Phantom:** "So the only implementable check lets any self-registered provider write to shared reference data. That raises the bar from 'any account' to 'any account that picked Provider at signup.'"
> **Neo:** "Then the strongest fix is to delete the route. Professions are seeded from `ProfessionSeedData.cs` and no shipped flow creates one. Removing surface beats guarding it."
> **Atlas:** "Or accept Provider-only for a pre-launch product on synthetic data. That's defensible. But it's the human's call, not ours."

---

## Layer 2 — Prioritization

| Threat | Severity | Decisive DREAD factor |
|---|---|---|
| T-002 | **CRITICAL** | Damage **H** — irreversible, **no backups**; Reproducibility **H** — one environment variable; the failure is *silent* (tests pass) |
| T-001 | **HIGH** | Damage **H** — full third-party PII, and the bypass lands on the owner branch |
| T-003 | **HIGH** | Exploitability **H** — free, unverified, unthrottled self-registration is the only prerequisite |
| T-004 | MEDIUM | Reproducibility **H** — a malformed id in any path reaching `GetByIdAsync` |
| T-005 | MEDIUM | Discoverability **L** — invisible until an incident, which is what makes it worth fixing now |
| T-006 | MEDIUM (HIGH if it lands) | Discoverability **L for a reviewer** — the danger is that nothing catches it |
| T-007 | MEDIUM | Damage **M** — integrity of shared reference data, not confidentiality |
| T-008 | MEDIUM | Exploitability **M** — requires obtaining a token first |

**Pitch+vote:** not needed. Cross-talk converged on every threat within one round. The two genuinely unresolved items (T-007's option, T-003's depth) are unresolved because they need **product context the party does not have**, not because agents disagreed — so they are escalated as open questions rather than voted on.

---

## Layer 3 — Proposals

| Bucket | Threats |
|---|---|
| **Mitigate now** | T-001, T-002, T-003, T-004, T-005, T-006, T-007 — seven, each with a binary `[security]` acceptance criterion drafted |
| **Mitigate later** | T-008 → **F-023 `token-revocation`**, whose feature record already names the `aud`/`ValidateAudience` decision as in scope. Requires an ADR. |
| **Accept** | none proposed |
| **Transfer** | none applicable |

**Dissents recorded:**
- **Friday** on T-005: adding a field to a persisted document costs this feature its clean no-migration rollback, which is one of its better properties for a change touching authorization across five services. Did not block — the finding stands — but wanted the trade named. It is named, as open question 3.
- **Atlas** on T-007: prefers accepting `Provider`-only over deleting the route, on the grounds that deletion is a product decision disguised as a security fix. Neo prefers deletion. Unresolved by design → open question 2.

---

## Open questions escalated to the human

1. **T-003** — is `GET /api/v1/customers` role-scoped, and how far? (`Provider` role · owner-scoped results · accept as-is). **Scope beyond the approved PRD.**
2. **T-007** — delete `POST /api/v1/professions`, add an `Admin` role, or accept `Provider`-only?
3. **T-005** — add `actor` to `Event`, or accept the accountability regression? Costs the no-migration property.
4. **T-002** — must the fail-closed guard hold in CI as well as locally? Confirm no CI path supplies a shared database.
5. **Governing context, not a threat** — `ISSUE-002`: the Atlas credential is still valid and still recoverable from this **public** repo's history. It is the main reason T-002 is CRITICAL. Rotation is human-only and outside this feature.

---

## Process notes

- **Five of eight threats are created or made reachable by this feature**, which is the outcome Phantom's framing was aiming for. A party that only re-derived the PRD's known defects would have added nothing.
- **Two findings changed the design rather than annotating it:** T-001 reassigns PRD requirement 18 into this feature, and T-003 proposes a scope addition the PRD does not authorize. Both go to the human.
- **One finding was produced by attempting implementation** (T-007). Worth noting as a pattern: Bolt's "I went to write it and the primitive doesn't exist" is a category of finding no amount of document review produces.
- The threat model is a **living document** — Phantom re-checks it in the Review sub-phase, and any implementation that introduces a boundary not modelled here is design drift for Neo to arbitrate.
