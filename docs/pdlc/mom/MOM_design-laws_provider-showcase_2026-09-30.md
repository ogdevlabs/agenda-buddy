---
feature: provider-showcase
topic: design-laws
date: 2026-09-30
mode: solo
lead: Muse
participants: Muse, Neo, Atlas, Bolt, Friday, Echo, Phantom, Pulse, Jarvis
---

# Meeting Minutes: Design-Laws Roundtable
## Feature: provider-showcase (F-037) | 2026-09-30

**Triage:** Full (3 of 3)
- New UI surface: 7 views plus chips on 3 existing ones.
- New flows: scan → showcase → book, and reorder, viewer and export.
- First-experience pathway: the QR install path, the Dashboard first-run card, and the empty states.

## Scorecards

- **Nielsen:** 28/40, Good.
  - Lowest: H3 = 2 (no undo on remove) and H10 = 2 (the two-scan journey is unexplained).
  - Contributions: Bolt and Friday on H4 (every pattern is already in the app); Atlas on H2 (funnel vocabulary); Phantom on H5 (the server cap is the authority; the picker should prevent before it refuses).
- **Audit:** 14/20, Good. A11y = 2 (drag-only reorder, cover contrast, unlabeled icons).
- **Cognitive load:** 1/8, acceptable. Item 7 fails on the two-scan journey.
- **8-state gaps:** portfolio tile focus; remove success; Report, Hide and Export outcomes; icon-button focus.
- **Anti-patterns:** one, the hero-metric funnel card.

## Findings and party recommendation

| ID | Sev | Finding | Recommendation |
|---|---|---|---|
| F-001 | P1 | Gendered copy ("her work", "her code") | Fix now + a structural test |
| F-002 | P1 | Two-scan journey unexplained before install | Fix now: code and two steps on every export, card and chooser |
| F-003 | P1 | Reorder and cover are drag-only | Fix now: per-tile `⋯` menu |
| F-004 | P2 | Remove has no undo | Fix now: Undo snackbar |
| F-005 | P2 | Picker over-selects | Fix now: limit = remaining slots |
| F-006 | P2 | Hero-metric funnel | Fix now: one sentence |
| F-007 | P2 | Report / Hide / Export outcome states | Fix now |
| F-008 | P2 | Text over photo cover | Fix now: scrim |
| F-009 | P2 | Raw error codes | Fix now: copy map, tested per code |
| F-010 | P2 | Unlabeled icon buttons | Fix now + a structural test |

There are no P0s. Every finding is a Plan-phase task; none needs an ADR.

## Cross-talk highlights

1. **Atlas × Muse (F-002):** the second scan drives attribution, so an unexplained journey hides a working QR behind a zero funnel. Raised from P2 to P1.
2. **Friday × Muse (F-003):** MAUI has no portable custom accessibility actions, so a visible `⋯` menu is the accessible path. Folding Remove into it removes a small `×` target.
3. **Phantom × Neo × Muse (F-004):** undo re-attaches the owner's own hash within the 24 h sweep window. No new route, and T-373 holds.
4. **Echo (F-001, F-010):** both get structural tests, so the defect class can't return.

## Dissent

Bolt preferred a confirmation dialog over undo for Remove. The non-binding vote was undo 6, confirm 2; the human confirms at Step 12.

## Open questions for the human

1. The es-MX term for "Showcase" (*vitrina*, *escaparate* or *portafolio*).
2. Adding the instruction text to the share art.
3. Undo rather than a confirmation for Remove.

## Actions

- **Neo:** at Plan, turn F-001 to F-010 into tasks with ACs.
- **Echo:** the gendered-copy and icon-description structural tests.
- **Jarvis:** fix the example copy in the brainstorm record.
