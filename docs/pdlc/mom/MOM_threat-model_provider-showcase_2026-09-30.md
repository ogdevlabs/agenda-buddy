---
feature: provider-showcase
topic: threat-model
date: 2026-09-30
mode: solo
lead: Phantom
participants: Phantom, Neo, Bolt, Echo, Pulse, Atlas, Muse, Jarvis, Friday
---

# Meeting Minutes: Threat Modeling Party
## Feature: provider-showcase (F-037) | 2026-09-30

**Triage:** Full (3 of 3)
- New trust boundaries: the anonymous `/go` route, blob egress, and binary content from users.
- Regulated data: face photos, GPS metadata in raw uploads, visit records.
- New attack surface: the upload parser, the redirect, 14 routes, the lookup, the scanner, and user-generated content.

## Layer 1 — Surfaced threats (STRIDE per boundary)

| Boundary | Agent | STRIDE | Raw threat → ID |
|---|---|---|---|
| TB-1 | Phantom | I | `/go` hit vs miss difference → T-377 |
| TB-1 | Phantom | S | `/go` open redirect if the target is ever supplied by the caller → T-378 |
| TB-1 | Pulse | D | `/go` flood → T-NL-3 |
| TB-2 | Bolt | E/T | Attaching another provider's hash → T-373 |
| TB-2 | Phantom | E | Customer calls authoring routes → T-373 |
| TB-2 | Phantom | I | `lookup` as an email oracle → T-376 |
| TB-2 | Pulse | D | Upload spam and storage cost → T-379 |
| TB-2 | Echo | R | A provider denies uploading → T-382 |
| TB-2 / TB-3 | Bolt, Pulse | D/E | Decompression bomb, decoder CVE → T-371 |
| TB-2 → TB-5 | Muse, Phantom | I | GPS in EXIF → T-372 |
| TB-4 | Pulse | E | Role assigned at account scope → T-383 |
| TB-4 | Echo | I | Blob outlives erasure → T-374 |
| TB-5 | Atlas, Muse | I / legal | Abusive or illegal content → T-375 |
| TB-5 | Phantom | T | Polyglot sniffing → T-381 |
| TB-5 (reverse) | Muse | I | Funnel exposes who viewed a provider → T-380 |
| TB-2 | Neo | I | Any signed-in user can fetch any media → T-384 |
| TB-6 | Friday | S | Malicious QR in the scanner → T-NL-1 |
| TB-7 | Jarvis | T | Injection in the report email → T-NL-2 |

**Chains found in cross-talk:**
1. **Upload DoS reaches customers.** Pulse pointed out that `/media` shares a replica with the directory, so a single bad upload (T-371) takes down customers who never use the feature. Rating raised from MEDIUM to HIGH.
2. **Report needs a takedown lever.** Atlas asked what happens after a Report. The operator had no lever short of erasing the account, so Muse and Atlas added a `hidden_by_operator` flag to T-375.
3. **Image theft.** Bolt found, while reviewing Neo's key-prefix design, that a hash visible in responses could be attached cross-account. Ownership of the `media_refs` record closes it (T-373).
4. **Erasure scope.** Echo noticed erasure only listed provider-side rows, so a customer's own visits and blocks would survive. Added to T-374's criterion.

## Layer 2 — Prioritisation

- **HIGH:** T-371, T-372, T-373, T-374, T-375.
- **MEDIUM:** T-376, T-377, T-378, T-379, T-380, T-381, T-382, T-383.
- **LOW–MEDIUM:** T-384.
- **LOW (recorded, not debated):** T-NL-1 to T-NL-6.

DREAD breakdowns are in `threat-model.md`.

## Layer 3 — Proposals

- **Mitigate now:** T-371 to T-383 (13 threats), each with a testable criterion.
  - **T-375 is split:** report, hide and takedown are mitigated now; automated scanning is mitigated later, recorded as ADR-072.
- **Accept:** T-384, recorded in ADR-069. Atlas's reasoning: promotional content, and relationship gating would break Preview and the scan-first flow.
- **Dissent:** Echo wanted an artificial constant delay on `/go` to remove the timing side channel. Phantom and Neo judged it disproportionate given the rate limit and the small value of what leaks. It stays recorded as T-NL-4. Non-binding.

## Open questions for the human

1. US distribution and CSAM obligations, which decide the timing of T-375's scanning.
2. The report inbox address and turnaround, and whether a database-write takedown is acceptable.
3. Whether profile photos count as biometric data.
4. Confirmation of the T-384 accepted risk.

## Actions

- **Neo:** at Plan, turn each "mitigate now" threat into a `[security]` AC.
- **Pulse:** the dedicated media Storage account, with public access and shared-key access both disabled.
- **Jarvis:** ADR-069 and ADR-072.
