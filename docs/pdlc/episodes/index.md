# Episodes

Delivery records, one per shipped feature. Permanent — episodes are never archived or rewritten.

> **Location convention.** This project keeps episodes in `docs/pdlc/episodes/`, **not** the PDLC template's
> default `docs/pdlc/memory/episodes/`. Episode 001 established it and 002 follows. Created retroactively at
> the 002 Reflect gate, 2026-08-22 — the two existing episodes predate this index.

| Episode | Feature | Date | File | PR | Status |
|---------|---------|------|------|----|--------|
| 001 | aspire-wiring | 2026-08-18 | [EPISODE_aspire-wiring_2026-08-17.md](EPISODE_aspire-wiring_2026-08-17.md) | #35 | Shipped (`v0.1.0`) |
| 002 | secure-public-endpoints | 2026-08-18 | [EPISODE_secure-public-endpoints_2026-08-18.md](EPISODE_secure-public-endpoints_2026-08-18.md) | #38 | Shipped (`v0.2.0`) |
| 003 | identity-hardening | 2026-08-22 | [EPISODE_identity-hardening_2026-08-22.md](EPISODE_identity-hardening_2026-08-22.md) | #39 | **Draft — built, CI green, not merged** |

**Naming:** `EPISODE_<feature-slug>_<YYYY-MM-DD>.md`, where the date is when the episode was opened, not
when the feature shipped — 002 opened 2026-08-18 and its ship gate closed 2026-08-22.

**Not tracked here:** F-001–F-012 are marked `Shipped` in `ROADMAP.md` but predate PDLC ship tracking
entirely — no episodes, no CHANGELOG entries, no tags. `v0.1.0` is the first tracked release.
