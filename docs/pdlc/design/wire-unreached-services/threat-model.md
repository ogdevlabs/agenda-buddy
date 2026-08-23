# Threat Model — wire-unreached-services (F-014)
<!-- pdlc-template-version: 1.2.0 -->

**Date:** 2026-08-23 · **Lead:** Phantom (Security Reviewer) · **Tier:** Full · **Status:** Approved; all eight dispositioned
**Design under review:** [`ARCHITECTURE.md`](ARCHITECTURE.md) · [`data-model.md`](data-model.md) · [`api-contracts.md`](api-contracts.md)

> ⚠️ **Ran in `solo` mode** — one model reasoning as each role, because this session carries a standing
> instruction not to spawn agents. Fidelity is lower than independent context windows. Same condition as
> every F-016 and F-021 meeting; recorded rather than glossed.

---

## Triage Record

| Question | Answer | Evidence |
|---|---|---|
| Does this feature introduce or modify a trust boundary? | **yes** | Nine new authenticated routes across three services, and it moves appointment status from caller-owned to server-owned — a change in who is trusted to assert state (`ARCHITECTURE.md` §3) |
| Does this feature touch regulated data (PII, payment, health, biometric, children's)? | **yes**, all three of the first three | **Session notes** about named individuals — for a therapist or coach these are health-adjacent; **payment records** with amounts and a live gateway credential; **message bodies** between two named people. Every one of the four new collections is keyed by an email address, which is PII under `CONSTITUTION.md` §4 |
| Does this feature add a new attack surface? | **yes** | It is the largest single expansion of the authenticated surface since F-016 halved it: four collections that have never been written, six route families, and a payment credential |

**Triage tier: 3/3 → Full.**

---

## Trust Boundaries

| ID | Boundary | What crosses | Direction | Reference |
|---|---|---|---|---|
| TB-1 | Authenticated client → Booking notes routes | Session-note content about a third party (the customer) | semi-trusted → trusted | `api-contracts.md` §1 |
| TB-2 | Authenticated client → Booking payment routes | Amounts, currency, and a charge instruction | semi-trusted → trusted | §2 |
| TB-3 | Authenticated client → Booking status route | An assertion about appointment state | semi-trusted → trusted | §3 |
| TB-4 | Authenticated client → Customer message/notification routes | Message bodies, and an implicit claim to be a thread participant | semi-trusted → trusted | §4, §5 |
| TB-5 | Booking → Stripe | A payment credential and an amount | trusted → untrusted (egress) | `ARCHITECTURE.md` §4 |
| TB-6 | Provider → deactivation command | A destructive state change | semi-trusted → trusted | §7 |

---

## Threats Identified

### T-201 — Session-note disclosure through an unscoped or client-scoped notes route

- **STRIDE:** Information Disclosure
- **Boundary:** TB-1
- **Asset:** Therapy/coaching session notes about named individuals — the most sensitive data the product holds
- **Attack vector:** The natural implementation of `GET …/notes` takes `providerEmail` as a parameter, because `NoteService.GetByAppointmentAsync(providerEmail, appointmentIdentifier)` asks for it. A route that passes a client-supplied value straight through gives any authenticated caller every provider's notes for any appointment identifier they can guess — and identifiers are `Guid`s that appear in appointment responses the customer already receives. **This is F-016's defect exactly**: a service that takes an owner parameter, called from a route that trusts the caller for it.
- **Severity:** **HIGH**
- **DREAD:** Damage [H — health-adjacent notes about third parties] · Reproducibility [H] · Exploitability [H — one query parameter] · Affected users [all providers and their clients] · Discoverability [M]
- **Frameworks:** OWASP API Top 10 **API1:2023 Broken Object Level Authorization** · CWE-639 · CWE-200
- **Current status:** N/A — introduced by this feature.
- **Action: Mitigate now.** The provider email is read from the caller's `sub` claim and **never** from the request; a body or query carrying one is ignored. Plus `AssertRole(user, "Provider")` and ownership of the appointment.
  - **Testable AC:** Given a valid token for provider A, when notes for provider B's appointment are requested by any means the route accepts, then the response is 403 and no note content is returned. *(PRD AC-9, AC-10)*
- **Decision (human):** **approved — mitigate now**

### T-202 — Note existence oracle

- **STRIDE:** Information Disclosure
- **Boundary:** TB-1
- **Asset:** The fact that notes exist for a given appointment — which for a therapist implies the session happened and was noteworthy
- **Attack vector:** `NoteService` throws `KeyNotFoundException` for a missing note and `UnauthorizedAccessException` for someone else's. Mapping those to `404` and `403` respectively lets a caller enumerate note ids and learn which exist. The identifiers are `ObjectId`s — not guessable at random, but they leak in any client-side log, screenshot or shared URL.
- **Severity:** **MEDIUM**
- **DREAD:** Damage [M — metadata, not content] · Reproducibility [H] · Exploitability [M] · Affected users [providers] · Discoverability [L]
- **Frameworks:** OWASP A01:2021 · CWE-209 (Information Exposure Through an Error Message)
- **Action: Mitigate now.** Both exceptions map to **403**. Not-found and not-yours are indistinguishable in status and body (`api-contracts.md` §1).
  - **Testable AC:** Given a note id that does not exist and one belonging to another provider, when each is requested, then the two responses are identical. *(PRD AC-12)*
- **Decision (human):** **approved — mitigate now**
- **Cross-talk:** The same reasoning F-021 applied to a locked account versus a wrong password. Two different causes, one indistinguishable answer.

### T-203 — Appointment state forged by the client

- **STRIDE:** Tampering / Repudiation
- **Boundary:** TB-3
- **Asset:** The integrity of the appointment lifecycle, and everything derived from it — the provider's report, and any future invoicing
- **Attack vector:** `AppointmentStatus` is a public settable property on the entity bound from the `PUT` body, and `UpdateAppointmentCommandHandler:51` copies it. A customer can mark an appointment `Completed` seconds after creating it — asserting that work was delivered — or set it back to `Requested` to make a completed session disappear from the provider's count. `Book()`/`Complete()`, which hold the rules, are never called.
- **Severity:** **MEDIUM** (today unreachable: the mobile client cannot reach the backend. **HIGH the moment F-015 lands**)
- **DREAD:** Damage [M–H — a disputed record of work delivered] · Reproducibility [H] · Exploitability [H — one JSON field] · Affected users [all] · Discoverability [H once any client exists]
- **Frameworks:** OWASP API Top 10 **API3:2023 Broken Object Property Level Authorization** · CWE-915 (Improperly Controlled Modification of Dynamically-Determined Object Attributes)
- **Current status:** Present today, unreachable by accident.
- **Action: Mitigate now.** Status becomes server-owned: ignored on the `PUT`, changed only through a dedicated route, applied through the entity's own transition methods, with completion restricted to the provider.
  - **Testable AC:** Given a `PUT` carrying `Completed` on a `Requested` appointment, the stored status remains `Requested`; and given a customer completing their own appointment, the response is 403. *(PRD AC-13, AC-16)*
- **Decision (human):** **approved — mitigate now**

### T-204 — Thread access by a non-participant

- **STRIDE:** Information Disclosure
- **Boundary:** TB-4
- **Asset:** Private message bodies between two named people
- **Attack vector:** `MessageService.GetThreadAsync(senderEmail, recipientEmail)` derives `thread_id` from **both** parameters. A route that takes both from the request lets any authenticated caller read any two people's thread by naming them — and the addresses are discoverable (a provider's email is in the discovery list F-016 authenticated but did not hide).
- **Severity:** **HIGH**
- **DREAD:** Damage [H — the full conversation] · Reproducibility [H] · Exploitability [H — two known addresses] · Affected users [all] · Discoverability [M]
- **Frameworks:** OWASP API1:2023 · CWE-639
- **Action: Mitigate now.** The route takes **one** counterpart address; the other side is always the caller's `sub` claim. One participant is structurally the caller, so an unrelated thread has no representation in the URL space. Same rule for the inbox: no recipient parameter at all.
  - **Testable AC:** Given a valid token, when a thread between two other people is requested by any route this feature exposes, then no such request can be expressed; and the inbox contains only the caller's messages with another principal's messages present in the same database. *(PRD AC-11)*
- **Decision (human):** **approved — mitigate now**

### T-205 — Payment forged, duplicated, or charged to the wrong participant

- **STRIDE:** Tampering / Elevation of Privilege
- **Boundary:** TB-2
- **Asset:** Payment records, and — once a key is configured — real money
- **Attack vector:** Three distinct holes in the obvious implementation. (a) `providerEmail`/`customerEmail` taken from the request body rather than the appointment lets a caller record a payment against anyone. (b) No uniqueness check lets the same appointment be charged repeatedly. (c) `amount` is entirely client-supplied and there is nothing to validate it against, because **an appointment does not record which service it is for** — the same gap that makes revenue uncomputable (Discover F-5). A customer can pay 0.01 for a 50 session and the record will say `Succeeded`.
- **Severity:** **MEDIUM** — (a) and (b) are closable now; **(c) is not**
- **DREAD:** Damage [M now, H if a key is ever configured] · Reproducibility [H] · Exploitability [H] · Affected users [providers] · Discoverability [M]
- **Frameworks:** OWASP API3:2023 · API6:2023 (Unrestricted Access to Sensitive Business Flows) · CWE-840 (Business Logic Errors)
- **Action: Mitigate (a) and (b) now; ACCEPT (c), documented.** Both participant emails come from the **stored appointment**, never the body; `AssertOwnerAny` restricts the call to those two; a second charge for the same appointment answers **409**. **(c) cannot be fixed without the appointment→service reference**, so `amount` stays client-asserted and that is stated in the contract rather than implied.
  - **Testable AC:** Given a payment request naming a different provider or customer, those values are ignored and the stored appointment's participants are used; and a second charge for the same appointment returns 409. *(PRD AC-6, AC-9)*
  - **Residual risk:** the amount is unvalidated. With the non-charging gateway this corrupts a record; with a real key it would be a real underpayment. **Anyone configuring `Payments:Stripe:ApiKey` must read this line first.**
- **Decision (human):** **approved — mitigate (a)+(b), accept (c) with the residual recorded**

### T-206 — The payment credential

- **STRIDE:** Information Disclosure
- **Boundary:** TB-5
- **Asset:** A live Stripe secret key
- **Attack vector:** Two ways this project has already been burned. **Committing it:** `ISSUE-002` is this repository's standing proof that a secret in git is permanent — an Atlas credential removed from the working tree in F-013 is still valid and still recoverable. **Leaking it at runtime:** `StripePaymentGateway` sets `StripeConfiguration.ApiKey`, a process-global static, inside request handling; a global holding a live credential is one bad log line or one exception-formatter change from being exported.
- **Severity:** **MEDIUM** (no key exists yet — this is entirely about not creating the conditions)
- **DREAD:** Damage [H if it happens] · Reproducibility [H — one `appsettings.json` edit] · Exploitability [n/a] · Affected users [the project owner's Stripe account] · Discoverability [H — the repository is public]
- **Frameworks:** OWASP A05:2021 · CWE-798 (Use of Hard-coded Credentials) · CWE-532
- **Action: Mitigate now.** The key is an **Aspire secret parameter**, exactly as the two JWT keys are: prompted once, stored in user secrets, masked in the dashboard, never in `appsettings.json`. Assigned to `StripeConfiguration` **once at construction**, not per request. And `Library.Tests`' existing PEM/secret hygiene test already scans tracked files — a committed key would have to pass a test that is looking for it.
  - **Testable AC:** Given the repository, no tracked file contains a Stripe secret key pattern; and given no configured key, the non-charging gateway is the one registered. *(PRD AC-17)*
- **Decision (human):** **approved — mitigate now**

### T-207 — Provider deactivation as a denial of service

- **STRIDE:** Denial of Service
- **Boundary:** TB-6
- **Asset:** A provider's business presence
- **Attack vector:** A deactivation route reachable by anyone but the provider — or by a provider naming a different email — takes a business offline. There is no administrative role in this product, so there is no legitimate caller other than the provider themselves.
- **Severity:** **MEDIUM**
- **DREAD:** Damage [H for the victim] · Reproducibility [H] · Exploitability [L once guarded] · Affected users [targeted] · Discoverability [M]
- **Frameworks:** OWASP API5:2023 (Broken Function Level Authorization) · CWE-285
- **Action: Mitigate now.** `Provider` role **and** ownership of the path email. No administrative bypass, because there is no administrator.
  - **Testable AC:** Given a customer token, or a provider token for a different email, when deactivation is requested, the response is 403 and no event is written. *(PRD AC-9, AC-10)*
- **Decision (human):** **approved — mitigate now**

### T-208 — Notification list poisoning

- **STRIDE:** Spoofing
- **Boundary:** TB-4
- **Asset:** Trust in what a notification says
- **Attack vector:** If a route allowed clients to create notifications, any authenticated caller could write a convincing "Your appointment was cancelled" into somebody else's list. Notifications are produced by domain events, not by users.
- **Severity:** **LOW** (by design, not by luck — see the action)
- **DREAD:** Damage [M] · Reproducibility [H] · Exploitability [n/a — the route does not exist] · Affected users [all] · Discoverability [L]
- **Frameworks:** OWASP A04:2021 (Insecure Design) · CWE-345
- **Action: Mitigate by omission, stated.** F-014 exposes **only the read side**. There is no create route, and `SendAsync` is reachable in-process only. The consequence — the list is empty until something writes one — is written into the contract so an empty `GET` is not read as a bug.
  - **Testable AC:** Given the Customer service's route table, no route creates a notification. *(PRD AC-11's companion; asserted as a route-table check)*
- **Decision (human):** **approved — mitigate by omission**

---

## Threats Noted but Not Prioritized

| ID | Title | STRIDE | Boundary | Why deprioritized |
|---|---|---|---|---|
| T-NL-1 | Notes and messages are stored unencrypted at rest | Info Disclosure | TB-1, TB-4 | True of every collection in this product, including `credentials`. Field-level encryption is a platform decision, not a wiring feature's, and the cluster holds synthetic data. It does mean the unrotated Atlas credential (`agenda-buddy-41s`) now also reaches session notes — which **raises the stakes of rotation** without changing what F-014 can do about it |
| T-NL-2 | No rate limit on message sending | DoS / Abuse | TB-4 | F-021's limiter covers `login` and `register`, the two routes that spend BCrypt. Messaging is cheap per request; the abuse case is spam between two consenting parties, which needs a product answer (blocking, reporting) rather than a limiter |
| T-NL-3 | Message bodies are unbounded | DoS | TB-4 | `MessageEntity.Body` has no length constraint, so a caller can store a 16 MB document up to MongoDB's limit. Real, cheap to fix, and out of scope: fixing it means editing a `Library` entity this feature is only wiring. Filed |
| T-NL-4 | New reads are uncached, so they are slower than they could be | — | — | Deliberate (ADR D-10). Cache invalidation does not exist anywhere (`agenda-buddy-xrw`); a cached inbox would show five-minute-old messages. Correctness over latency, revisit when invalidation exists |
| T-NL-5 | Cancellation still hard-deletes | Repudiation | TB-3 | An appointment cancelled leaves no record that it existed, so a dispute has no evidence. `AppointmentStatus.Cancelled` exists in the enum and is never used. F-024 owns erasure; changing it here would be a soft-delete migration inside a wiring feature |

---

## Open Questions for Human

1. **T-205(c): the payment amount is unvalidated, and cannot be validated** until an appointment records
   which service it is for. Accept for now — with the residual recorded in the contract — or block payments
   until the data model supports a price? *(Recommendation: accept. The non-charging default means a wrong
   amount corrupts a record rather than a transaction, and blocking would leave the sixth capability
   unreachable — the condition this feature exists to end.)*
2. **T-NL-3: message bodies are unbounded.** File it, or add a `[MaxLength]` and accept that F-014 edited an
   entity? *(Recommendation: file. The moment this feature starts editing service and entity internals, its
   claim — "these capabilities work as written, they were merely unreachable" — stops being verifiable.)*
3. **T-NL-1 raises the stakes of credential rotation**: `agenda-buddy-41s` has been open across three
   releases, and after F-014 the cluster that credential reaches contains session notes rather than only
   names and appointment times. It is still synthetic data today. Worth re-reading the P0 with that in mind.

---

## Approval Outcomes

| Threat | Party recommendation | Human decision | Rationale |
|---|---|---|---|
| T-201 | Mitigate now | **Mitigated** | Provider email from the `sub` claim only; `Provider` role; appointment ownership |
| T-202 | Mitigate now | **Mitigated** | Not-found and not-yours both answer 403, indistinguishably |
| T-203 | Mitigate now | **Mitigated** | Status server-owned; transitions through the entity; completion provider-only |
| T-204 | Mitigate now | **Mitigated** | One counterpart in the URL, the other always the caller's claim |
| T-205 | Mitigate (a)+(b), accept (c) | **Mitigated / Accepted** | Participants from the stored appointment; 409 on a second charge; **amount stays unvalidated and is documented** |
| T-206 | Mitigate now | **Mitigated** | Aspire secret parameter, never `appsettings.json`; static assigned once at construction |
| T-207 | Mitigate now | **Mitigated** | `Provider` role plus ownership; no administrative bypass because no administrator exists |
| T-208 | Mitigate by omission | **Mitigated** | Read-side only; asserted as a route-table check |

---

## Mitigation → Task → `[security]` AC mapping

| Threat | Task (at Plan) | Testable `[security]` AC |
|---|---|---|
| T-201 | Notes routes | PRD **AC-9**, **AC-10** |
| T-202 | Notes error mapping | PRD **AC-12** |
| T-203 | Server-owned status | PRD **AC-13**, **AC-16** |
| T-204 | Message routes | PRD **AC-11** |
| T-205 | Payment routes | PRD **AC-6**, **AC-9** |
| T-206 | Gateway registration | PRD **AC-17** |
| T-207 | Deactivation route | PRD **AC-9**, **AC-10** |
| T-208 | Notification routes | route-table assertion |

Threat IDs continue from F-021's T-101…T-106 series, starting at **T-201** so the feature boundary is
unambiguous in test names (`test_T201_…`).

---

## Revision History

| Date | Author | Change |
|---|---|---|
| 2026-08-23 | Phantom (solo) | Created at Step 10.5. Triage 3/3 → Full. Eight threats, seven mitigated now and one partially accepted; five deprioritized |
