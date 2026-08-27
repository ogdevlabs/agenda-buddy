# Episodes

Delivery records, one per shipped feature. Permanent — episodes are never archived or rewritten.

> **Location convention.** This project keeps episodes in `docs/pdlc/episodes/`, **not** the PDLC template's
> default `docs/pdlc/memory/episodes/`. Episode 001 established it and 002 follows. Created retroactively at
> the 002 Reflect gate, 2026-08-22 — the two existing episodes predate this index.

| Episode | Feature | Date | File | PR | Status |
|---------|---------|------|------|----|--------|
| 001 | aspire-wiring | 2026-08-18 | [EPISODE_aspire-wiring_2026-08-17.md](EPISODE_aspire-wiring_2026-08-17.md) | #35 | Shipped (`v0.1.0`) |
| 002 | secure-public-endpoints | 2026-08-18 | [EPISODE_secure-public-endpoints_2026-08-18.md](EPISODE_secure-public-endpoints_2026-08-18.md) | #38 | Shipped (`v0.2.0`) |
| 003 | identity-hardening | 2026-08-22 | [EPISODE_identity-hardening_2026-08-22.md](EPISODE_identity-hardening_2026-08-22.md) | #39 | Shipped (`v0.3.0`) |
| 004 | wire-unreached-services | 2026-08-23 | [EPISODE_wire-unreached-services_2026-08-23.md](EPISODE_wire-unreached-services_2026-08-23.md) | #40 | Shipped (`v0.4.0`) |
| 005 | api-gateway-and-mobile-contract | 2026-08-24 | [EPISODE_api-gateway-and-mobile-contract_2026-08-24.md](EPISODE_api-gateway-and-mobile-contract_2026-08-24.md) | #41 | Shipped (`v0.5.0`) |
| 006 | container-and-cd-hardening | 2026-08-26 | ⚠️ `docs/pdlc/memory/episodes/006_container-and-cd-hardening_2026-08-26.md` — wrong location, see note below | #48 | Shipped (`v0.6.0`) |
| 007 | api-refactor-foundations | 2026-08-26 | ⚠️ `docs/pdlc/memory/episodes/007_api-refactor-foundations_2026-08-26.md` — wrong location, see note below | #69 | Shipped (`v0.7.0`) |
| 008 | api-refactor-pilot-booking | 2026-08-27 | [EPISODE_api-refactor-pilot-booking_2026-08-27.md](EPISODE_api-refactor-pilot-booking_2026-08-27.md) | none — merged directly (`fb91cb1`), see episode's Links section | Shipped (`v0.8.0`) |
| 009 | api-refactor-rollout | 2026-08-27 | [EPISODE_api-refactor-rollout_2026-08-27.md](EPISODE_api-refactor-rollout_2026-08-27.md) | none — merged directly, see episode's Links section | Shipped (`v0.9.0`) |
| 010 | booking-correctness | 2026-08-27 | [EPISODE_booking-correctness_2026-08-27.md](EPISODE_booking-correctness_2026-08-27.md) | #72 | Shipped (`v0.10.0`) |
| 011 | password-reset-flow | 2026-08-27 | [EPISODE_password-reset-flow_2026-08-27.md](EPISODE_password-reset-flow_2026-08-27.md) | #77 | Shipped (`v0.11.0`) |
| 012 | provider-subscription | 2026-08-27 | [EPISODE_provider-subscription_2026-08-27.md](EPISODE_provider-subscription_2026-08-27.md) | #80 (corrected here — the episode's own file said #79, which is actually F-023's PR) | Shipped (`v0.12.0`) |
| 013 | token-revocation | 2026-08-27 | [EPISODE_token-revocation_2026-08-27.md](EPISODE_token-revocation_2026-08-27.md) | #79 | Shipped (`v0.13.0`) |
| 014 | carter-route-modules | 2026-08-27 | [EPISODE_carter-route-modules_2026-08-27.md](EPISODE_carter-route-modules_2026-08-27.md) | #83 | Shipped (`v0.14.0`) |

**Naming:** `EPISODE_<feature-slug>_<YYYY-MM-DD>.md`, where the date is when the episode was opened, not
when the feature shipped — 002 opened 2026-08-18 and its ship gate closed 2026-08-22.

**⚠️ Location drift, episodes 006–007.** This index was stale (missing row 005) since episode 001, backfilled
at F-019's Ship. In that same gap, episodes 006 and 007 were written to the PDLC template's *default* path
(`docs/pdlc/memory/episodes/`) instead of this project's own established convention
(`docs/pdlc/episodes/`, set by episode 001) — a real process regression, not fixed retroactively here (both
are shipped, permanent records; moving them now would rewrite delivered history for no behavioral gain).
Episode 008 restores the correct location. If this recurs, it's worth promoting from a footnote to an
actual guard.

**Not tracked here:** F-001–F-012 are marked `Shipped` in `ROADMAP.md` but predate PDLC ship tracking
entirely — no episodes, no CHANGELOG entries, no tags. `v0.1.0` is the first tracked release.
