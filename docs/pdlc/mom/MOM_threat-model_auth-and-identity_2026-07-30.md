# MOM — Threat Modeling Party: auth-and-identity
**Date:** 2026-07-30
**Called by:** Phantom (Security Reviewer)
**Participants:** Phantom, Neo, Bolt, Echo, Pulse, Atlas, Muse, Jarvis, Friday
**Feature:** F-001 auth-and-identity
**Triage outcome:** Full (3/3)

---

## Triage Record

| Question | Answer |
|---|---|
| Trust boundary changes | Yes — brand new auth boundary for the entire platform |
| Regulated data | Yes — email (PII), bcrypt password hashes, session refresh tokens |
| New attack surface | Yes — 4 Identity endpoints + JWT middleware across 6 consumer services |

---

## Layer 1 — Threats Surfaced (divergent)

| ID | Boundary | STRIDE | Threat | Contributing agent(s) |
|---|---|---|---|---|
| T-001 | TB-1 | EoP | Brute-force / credential stuffing on `/auth/login` | Phantom, Pulse |
| T-002 | TB-1, TB-3 | Spoofing, EoP | RSA private key compromise → arbitrary JWT forgery | Phantom, Neo |
| T-003 | TB-3 | Spoofing, EoP | `alg:none` / HS256 downgrade attack | Phantom |
| T-004 | TB-4 | EoP, InfoDisc | Missing handler-level ownership check (IDOR) | Phantom, Neo, Echo, Atlas |
| T-005 | TB-1 | InfoDisc | User enumeration via login timing side-channel | Phantom, Bolt |
| T-006 | TB-1, TB-2 | Tampering, EoP | NoSQL injection via email field | Phantom, Echo |
| T-007 | TB-1 | DoS | bcrypt amplification DoS on login endpoint | Pulse, Friday |
| T-008 | TB-3 | InfoDisc | PII (email) in JWT payload | Phantom, Atlas |

---

## Layer 2 — Prioritization (DREAD)

| ID | Severity | Damage | Reproducibility | Exploitability | Affected users | Discoverability |
|---|---|---|---|---|---|---|
| T-002 | CRITICAL | H | H | M | All | L |
| T-001 | HIGH | H | H | H | All | H |
| T-003 | HIGH | H | H | M | All | H |
| T-004 | HIGH | H | H | H | All | H |
| T-005 | MEDIUM | M | H | M | All | M |
| T-006 | MEDIUM | H | M | L | All | M |
| T-007 | MEDIUM | M | H | H | All | H |
| T-008 | MEDIUM | M | M | L | All | M |

---

## Layer 3 — Proposed Mitigations

| ID | Bucket | Proposal | Key contributors |
|---|---|---|---|
| T-001 | Accept / Mitigate later | bcrypt cost 12 is the current floor; rate limiting deferred to security-hardening feature. ADR required. | Atlas (business justification), Phantom (residual risk), Friday (timeline) |
| T-002 | Mitigate now | Startup fingerprint log; `.gitignore` for key files; deployment runbook key rotation. Low cost, high value. | Phantom, Neo, Bolt |
| T-003 | Mitigate now | Already in design (`ValidAlgorithms = ["RS256"]`). Echo adds regression tests for `alg:none` and HS256. | Phantom, Echo |
| T-004 | Mitigate now | `OwnershipGuard.AssertOwner()` helper in Library. Echo writes IDOR test per affected endpoint. Atlas enforces at task definition. | Neo, Echo, Atlas |
| T-005 | Mitigate now | Dummy bcrypt call on email-not-found path to normalize response time. ~5 lines of code. | Bolt, Phantom |
| T-006 | Mitigate now | Typed `FilterDefinition<T>` already in design. Echo adds NoSQL injection input test. | Phantom, Echo |
| T-007 | Accept / Mitigate later | Same rate-limiting feature as T-001. Pre-launch risk is low. bcrypt cost factor to be re-evaluated if p95 latency exceeds 500ms NFR. ADR required. | Pulse, Friday, Atlas |
| T-008 | Accept | Email-as-sub is standard JWT practice. Alternative (UUID sub) adds per-request lookup overhead on all ownership checks. PII-in-logs prohibition in CONSTITUTION.md is the guard. ADR required. | Atlas, Phantom |

---

## Cross-talk Highlights

**T-001 → T-007 chain (Pulse + Friday):**
> Pulse: "bcrypt at cost 12 under concurrent login load creates a CPU amplification risk — 50–100 parallel requests could saturate the Identity service."
> Friday: "Pre-launch this is low risk. But at scale, the same feature that fixes T-001 (rate limiting) also fixes T-007. They should land in the same hardening ticket."

**T-004 framing (Atlas):**
> "This is the highest-business-impact finding in the model. A provider reading another provider's client list is a trust-destroying incident that could sink the platform's reputation. The `OwnershipGuard` helper Neo proposed is exactly the right forcing function — it makes omitting the check a deliberate choice, not an easy accident."

**T-005 timing fix (Bolt):**
> "A single static dummy hash field and one conditional call in the login handler. I'll estimate 20 minutes of implementation time. It's the cheapest non-trivial security fix in the whole model."

---

## Open Questions for Human

1. Regulatory exposure (EU GDPR / US state privacy laws): does the target market require breach notification for email + credential data? Affects whether T-001 (no rate limiting) should be promoted to "mitigate now."
2. Threat-actor profile: opportunistic vs. targeted? If healthcare or legal professionals are the primary provider type, the threat profile warrants earlier rate limiting.

---

## Conclusion

Eight threats identified across four trust boundaries. Two accepted for v1 with ADRs (T-001, T-007); one accepted as standard practice with ADR (T-008); five marked "mitigate now" (T-002, T-003, T-004, T-005, T-006). The five "mitigate now" items are all low-to-medium implementation effort. The two deferred items (rate limiting, brute-force protection) share a future Beads task. No redesign required — the design is architecturally sound. Human decision pending at Step 12.
