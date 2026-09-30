# UX Review — provider-showcase (F-037)
<!-- pdlc-template-version: 1.5.0 -->

**Triage:** Full
**Convened:** 2026-09-30
**Lead:** Muse (UX Designer)
**Participants:** Muse, Neo, Atlas, Bolt, Friday, Echo, Phantom, Pulse, Jarvis
**Status:** Approved (Step 12, 2026-09-30) — es-MX term "Mi vitrina"; share-art instructions accepted; undo over confirm

---

## Triage Record

| Question | Answer | Evidence |
|---|---|---|
| Does this feature add or modify any user-facing UI surface? | yes | 7 new views (My Showcase hub, Photo/Logo, Tagline & About, Portfolio editor, QR & Share, ScanProviderPage, ProviderShowcasePage); rewired providers-list tap; chips on AppointmentDetail, BookAppointment, MessageThread |
| Does this feature introduce a new flow, page, or significant interaction pattern? | yes | scan → showcase → book; drag-to-reorder grid; full-screen swipe viewer; share-image export |
| Does this feature touch first-experience pathways (onboarding, first-time empty state, signup, install)? | yes | the QR's install path, the Dashboard first-run "Found a provider?" card, the empty showcase and empty hub |

**Triage outcome:** Full

---

## Heuristics Scorecard (Nielsen 10)

| # | Heuristic | Score (0-4) | Severity if ≤2 | Notes |
|---|---|---|---|---|
| 1 | Visibility of system status | 3 | — | Per-tile upload progress, completeness meter, post-scan snackbar. Report, Hide and Export do not yet define their success/failure states (F-007) |
| 2 | Match between system and real world | 3 | — | The funnel's words (`scans`, `opened`, `booked`) are ours, not the provider's (F-006) |
| 3 | User control and freedom | 2 | P2 | Removing a portfolio image is immediate and has no way back (F-004). Hide is reversible from More → Hidden providers, which is good |
| 4 | Consistency and standards | 3 | — | Composes ProfilePage hero, pinned actions, settings cards, BrandHeader back; no deviations |
| 5 | Error prevention | 3 | — | The server refuses the 21st image, but the picker lets you select 25 when 2 slots remain (F-005) |
| 6 | Recognition rather than recall | 3 | — | The printed code must be readable and forgiving to type (F-NL-1) |
| 7 | Flexibility and efficiency of use | 3 | — | Multi-select upload, reorder, three export formats, scan or type |
| 8 | Aesthetic and minimalist design | 3 | — | Magazine hero, ≤3 pinned actions, relationship banner replaces rather than adds. Text over a photo cover needs a scrim (F-008) |
| 9 | Help users recognize, diagnose, recover from errors | 3 | — | Distinct not-found vs network copy; upload rejection codes and `portfolio-changed` still need human copy (F-009) |
| 10 | Help and documentation | 2 | P1 | The two-scan journey (camera → store → install → register → scan again in the app) is explained nowhere the customer sees it before installing (F-002) |

**Total:** 28/40   **Health band:** Good (28-35) — address weak areas, ship

---

## Audit Scorecard (5 dimensions)

| # | Dimension | Score (0-4) | Severity if ≤2 | Notes |
|---|---|---|---|---|
| 1 | A11y | 2 | P1 | Reorder is drag-only, so VoiceOver/TalkBack users cannot set their cover or order (F-003); cover text contrast (F-008); icon-only buttons (F-010). Captions-as-descriptions and "Type a code instead" are already in |
| 2 | Performance | 3 | — | 480 px thumbs, disk cache, skeleton, on-device pre-resize before upload; the full-screen viewer fetches `full` per page only |
| 3 | Theming | 3 | — | App.xaml tokens only; the logo badge needs a `BackgroundCard` backplate over photo covers (F-NL-4) |
| 4 | Responsive | 3 | — | Full-bleed hero must respect the safe area under BrandHeader; touch targets ≥44 pt on grid tiles and chips |
| 5 | Anti-Patterns | 3 | — | One hit: the funnel as three big numbers (F-006) |

**Total:** 14/20   **Health band:** Good (14-17)

---

## 8-State Coverage Matrix

Touch-only client: **Hover** is n/a (—). **Focus** means screen-reader focus with a spoken description.

| Element | Default | Hover | Focus | Active | Disabled | Loading | Error | Success |
|---|---|---|---|---|---|---|---|---|
| Hub rows (Photo, Logo, Tagline & About, Portfolio, Preview, QR & Share) | ✓ | — | ✓ | ✓ | n/a | ✓ | ✓ | ✓ |
| Add work tile | ✓ | — | ✓ | ✓ | ✓ (20 of 20, with reason) | ✓ per tile | ✓ retry | ✓ |
| Portfolio tile (editor) | ✓ | — | ✗ F-003 | ✓ | n/a | ✓ | ✓ | ✓ |
| Remove image | ✓ | — | ✓ | ✓ | n/a | ✓ | ✓ | ✗ F-004 (no undo) |
| Photo / Logo pick | ✓ | — | ✓ | ✓ | n/a | ✓ | ✓ | ✓ |
| Tagline & About Save | ✓ | — | ✓ | ✓ | always enabled (validates in handler) | ✓ | ✓ | ✓ |
| Export Story / Square / Card | ✓ | — | ✗ F-010 | ✓ | n/a | ✗ F-007 | ✗ F-007 | ✓ share sheet |
| Scanner viewfinder | ✓ | — | ✓ | ✓ | ✓ (camera denied → Open Settings) | ✓ camera starting | ✓ foreign QR inline | ✓ snackbar |
| Type-a-code Open | ✓ | — | ✓ | ✓ | always enabled | ✓ | ✓ unknown vs network | ✓ |
| Pinned Book / Subscribe / Message | ✓ | — | ✓ | ✓ | ✓ (isSelf in Preview) | ✓ | ✓ | ✓ |
| Report | ✓ | — | ✗ F-010 | ✓ | ✓ (own showcase) | ✓ | ✗ F-007 | ✗ F-007 |
| Hide provider | ✓ | — | ✓ | ✓ | n/a | ✓ | ✗ F-007 | ✗ F-007 |
| "See {FirstName}'s work" chip + strip | ✓ | — | ✓ | ✓ | hidden when no work | ✓ | ✓ (strip hidden) | ✓ |
| Dashboard first-run card | ✓ | — | ✓ | ✓ | n/a | n/a | n/a | ✓ collapses to More row |
| Next-session banner Details / Reschedule | ✓ | — | ✓ | ✓ | n/a | ✓ | ✓ | ✓ |

Missing states are findings F-003, F-004, F-007, F-010.

---

## Cognitive Load Assessment

| # | Checklist item | Pass / Fail | Notes |
|---|---|---|---|
| 1 | Users can complete primary tasks without distraction | Pass | Scan → showcase → Book is 2 taps past the scan |
| 2 | Information presented in groups of ≤4 items | Pass | Hub regrouped into 3 cards (You / Your work / Share) at UX Discovery |
| 3 | Related elements visually grouped at a glance | Pass | |
| 4 | Screen priority immediately recognizable | Pass | Hero then work; Book pinned |
| 5 | User focused on one decision sequentially | Pass | |
| 6 | ≤4 visible options per decision | Pass | 3 pinned actions; Report and Hide in the `⋯` overflow |
| 7 | User can avoid referencing previous screens | Fail | The customer must carry "this provider, scan again" across an install and a registration (F-002) |
| 8 | Complexity revealed progressively | Pass | Completeness meter drives the provider one item at a time |

**Failure count:** 1/8   **Verdict:** acceptable (0-1)

---

## Persona Red-Flag Scan

| Persona | Profile (one line) | Red flags found | Blocking? |
|---|---|---|---|
| The first-time user | Scanned a poster, has never heard of AgendaMe | Lands in the store with no reason to scan again after installing (F-002) | no (fix now) |
| The accessibility-dependent user | VoiceOver/TalkBack, low vision, motor impairment | Drag-only reorder (F-003); text over photo (F-008); unlabeled icon buttons (F-010) | no (fix now) |
| The distracted-mobile user | Uploading between clients, flaky signal | Per-tile retry is in; leaving the editor mid-upload drops unfinished tiles (F-NL-2) | no |
| The edge-case stress-tester | Picks 25 photos, edits on two phones | Picker over-selection (F-005); `409 portfolio-changed` needs copy (F-009) | no |

---

## Anti-Patterns Found

| Pattern | Location | Severity | Proposed action |
|---|---|---|---|
| Hero-metric template | My Showcase funnel card ("41 · 17 · 4") | P2 | Replace — one plain sentence (F-006) |

Checked and clean: side-stripe borders, gradient text, glassmorphism, identical-card grids (the hub's three cards hold different things), modal-as-first-thought (no confirmation modals; see F-004), purple-blue AI hero (the brand gradient is the existing teal/amber `BrandGradient`).

---

## UX Writing Findings

| Surface | Issue | Severity | Proposed copy (EN / es-MX) |
|---|---|---|---|
| AppointmentDetail chip, BookAppointment strip, post-scan snackbar | "View her work", "through her code" assume the provider's gender | P1 | "See {FirstName}'s work" / "Ver el trabajo de {FirstName}"; "Opened from {FirstName}'s code" / "Abierto desde el código de {FirstName}" |
| Story / Square / Card exports | Nothing tells a new customer to scan again after installing | P1 | "1 · Scan to get AgendaMe  2 · In the app, tap Scan a code — or type {CODE}" / "1 · Escanea para descargar AgendaMe  2 · En la app, toca Escanear código — o escribe {CODE}" |
| Funnel card | Internal metric names | P2 | "This week, 41 people scanned your code, 17 opened your showcase and 4 booked." / "Esta semana, 41 personas escanearon tu código, 17 abrieron tu vitrina y 4 reservaron." — pluralised for 0 and 1 |
| Remove image | — | P2 | Snackbar "Removed from your portfolio · Undo" / "Se quitó de tu portafolio · Deshacer" |
| Hide provider | Must say what happens and how to undo | P2 | "Hide {FirstName}? You won't see them in your providers list or when scanning. Undo any time in More → Hidden providers." Buttons "Hide provider" / "Keep" |
| Upload rejections | Codes shown raw | P2 | `unsupported-format` "Use a JPEG, PNG or WebP photo."; `too-large`/`too-many-pixels`/`dimension-exceeded` "This photo is too large. Try a smaller one."; `undecodable` "This file couldn't be opened as a photo."; `503` "Photos can't be saved right now. Try again in a few minutes." |
| Reorder conflict | `409 portfolio-changed` | P2 | "Your portfolio changed on another device. We've reloaded it — try again." |
| Empty hub portfolio | "Add your first work" lacks the why | P3 | "Show your work — customers can see it before they book. Add photos" |
| Icon-only buttons | QR, share, `⋯` overflow | P2 | `SemanticProperties.Description`: "Show my QR code", "Share", "More options" |

---

## Findings & Proposed Actions

### F-001 — Copy assumes the provider's gender

- **Source:** UX-writing
- **Severity:** P1
- **Description:** "View her work" and "You found Mariana through her code" came from mockups built around one example provider. Shipped, they misgender every provider who isn't a woman, on the screens that are supposed to build trust in that person.
- **Catalog reference:** `muse-ux-design.md` → *UX Writing* (terminology, inclusive language)
- **Proposed action:** Fix now — name-based copy (table above). Echo adds a structural test that fails on `\b(her|his|she|he)\b` in user-facing strings under `AgendaBuddy.MobileApp`, the same shape as `BrandHeaderPresenceTest`'s old-name check.
- **Decision (human, at Step 12 approval):** Fix now — approved 2026-09-30.
- **Cross-talk note:** Echo proposed the test so it cannot regress; Jarvis to fix the brainstorm fragments' wording in the record.

### F-002 — The two-scan journey is invisible before install

- **Source:** Heuristic-10; Cognitive-load item 7; Persona: first-time user
- **Severity:** P1
- **Description:** Because nothing is visible signed out (ADR-070), a phone-camera scan only reaches the store. After installing and registering, the customer has to scan the same code again inside the app, and nothing they saw before installing told them so. The iOS/Android path is a bare 302, so the shared image is the only thing that can carry the instruction.
- **Catalog reference:** *Heuristics Scoring* → H10; *Cognitive Load* item 7
- **Proposed action:** Fix now —
  1. Every export (Story, Square, Printable card) carries the 6-character code in large type plus the two-step instruction (UX Writing table).
  2. The Dashboard first-run card reads "Found a provider? Scan their code or type it in."
  3. The static `/go` chooser page carries the same two steps (still provider-free and code-free).
- **Decision (human, at Step 12 approval):** Fix now — approved 2026-09-30.
- **Cross-talk note:** Atlas raised it from P2 to P1. Attribution and the north-star metric (booking within 7 days of a scan) both depend on that second scan, so if customers skip it the funnel reads zero while the QR is working.

### F-003 — Reorder and cover choice are drag-only

- **Source:** Audit-dimension: A11y; 8-state Focus gap on portfolio tile; Persona: accessibility-dependent
- **Severity:** P1
- **Description:** A screen-reader or switch user cannot drag, so they could never choose their cover (`portfolio[0]`) or order their work.
- **Catalog reference:** *Audit Scorecard* → A11y (WCAG 2.2 SC 2.5.7 Dragging Movements)
- **Proposed action:** Fix now — each tile gets a `⋯` menu: Make cover · Move earlier · Move later · Edit caption · Remove. It uses the same `PUT /portfolio/order` route, so no contract change. The tile's description reads "Photo 3 of 12, {caption}".
- **Decision (human, at Step 12 approval):** Fix now — approved 2026-09-30.
- **Cross-talk note:** Friday pointed out that MAUI has no cross-platform custom accessibility actions, and a visible menu reaches VoiceOver with no platform code. Muse added Remove to the same menu, so there's no separate small `×` target to mis-tap.

### F-004 — Removing an image has no way back

- **Source:** Heuristic-3; 8-state Success gap
- **Severity:** P2
- **Description:** Remove is immediate. A mis-tap loses the image, its caption and its position.
- **Proposed action:** Fix now — show "Removed from your portfolio · Undo" for 5 s. Undo re-`POST`s the same hash and caption, then restores the position via the order route. It works with no new route because the `media_refs` record survives detachment until the 24 h sweep. No confirmation modal.
- **Decision (human, at Step 12 approval):** Fix now — approved 2026-09-30.
- **Cross-talk note:** Phantom confirmed the re-add is the owner re-attaching their own hash, so ownership (T-373) holds. Bolt preferred a confirmation dialog as simpler. Non-binding vote: undo 6, confirm 2.

### F-005 — The picker can over-select

- **Source:** Heuristic-5; Persona: stress-tester
- **Severity:** P2
- **Proposed action:** Fix now — set `MediaPicker`'s selection limit to 20 − count, and show "18 of 20" in the editor header. The server's `409 portfolio-full` stays the authority.
- **Decision (human, at Step 12 approval):** Fix now — approved 2026-09-30.

### F-006 — Funnel shown as a hero-metric strip

- **Source:** Anti-pattern: hero-metric template; Heuristic-2
- **Severity:** P2
- **Proposed action:** Fix now — one sentence in the provider's words, pluralised for 0 and 1; zero-state copy "No one has scanned your code yet. Share it from QR & Share."
- **Decision (human, at Step 12 approval):** Fix now — approved 2026-09-30.
- **Cross-talk note:** Bolt: pluralisation needs a helper that handles the es-MX forms, not string concatenation.

### F-007 — Report, Hide and Export lack outcome states

- **Source:** 8-state coverage gap
- **Severity:** P2
- **Proposed action:** Fix now —
  - **Report:** success "Thanks — we'll review this." and closes; failure "Couldn't send your report. Try again."
  - **Hide:** success returns to the previous screen with a snackbar "{FirstName} is hidden · Undo".
  - **Export:** "Preparing image…" while rendering; failure "Couldn't create the image. Try again." A cancelled share sheet is not an error.
- **Decision (human, at Step 12 approval):** Fix now — approved 2026-09-30.

### F-008 — Name and tagline over a photo cover

- **Source:** Audit-dimension: A11y (contrast); Heuristic-8
- **Severity:** P2
- **Proposed action:** Fix now — a bottom scrim from the `BackgroundPage` token under the name and tagline, holding ≥4.5:1 against any cover. Verified at the as-built audit on a light and a dark cover.
- **Decision (human, at Step 12 approval):** Fix now — approved 2026-09-30.

### F-009 — Error codes need human copy

- **Source:** Heuristic-9
- **Severity:** P2
- **Proposed action:** Fix now — map every `errors.image` code, `503 storage-unavailable` and `409 portfolio-changed` to the copy in the UX Writing table. Unit-tested on the `net10.0` slice, with every code covered, the same way `TypeLabel` must name every enum member.
- **Decision (human, at Step 12 approval):** Fix now — approved 2026-09-30.

### F-010 — Icon-only buttons are unlabeled

- **Source:** Audit-dimension: A11y; 8-state Focus gap
- **Severity:** P2
- **Proposed action:** Fix now — add `SemanticProperties.Description` on every icon-only button in the 7 views, guarded by a structural test that reads the XAML.
- **Decision (human, at Step 12 approval):** Fix now — approved 2026-09-30.

---

## Findings Noted but Not Prioritized

| ID | Title | Source | Catalog reference | Why deprioritized |
|---|---|---|---|---|
| F-NL-1 | Code entry: auto-uppercase, strip spaces/dashes; print grouped "K7Q 2X9" | H6 | *Interaction* | The alphabet already excludes 0/O/1/I/L |
| F-NL-2 | Leaving the editor mid-upload drops unfinished tiles | Persona: distracted-mobile | — | The hub count shows the truth; a background queue is out of scope |
| F-NL-3 | es-MX name for "Showcase" | UX-writing | *Terminology* | Needs a native-speaker call (open question 1) |
| F-NL-4 | Logo badge needs a `BackgroundCard` backplate over photo covers | Theming | *Color* | Visual polish |
| F-NL-5 | Dashboard card has no manual "Not now" | H3 | — | It already collapses after the first subscription or booking |

---

## Open Questions for Human

1. **es-MX term for "Showcase":** "Mi vitrina", "Mi escaparate" or "Mi portafolio"? The copy above assumes *vitrina*.
2. **Instruction text on the share art (F-002):** it adds two lines to the Story and Square exports. Muse recommends it, because without it the QR mostly dead-ends at the store. Is that acceptable on the brand side?
3. **Undo instead of a confirmation for Remove (F-004):** confirm.

---

## Variant Convergence (Step 10.7)

**Outcome:** Skipped — trigger conditions not met.
**Rationale:** Full triage, but all four signals returned no: H8 = 3, H4 = 3, cross-talk converged on every finding, and no P1 is tagged "needs visual exploration" (F-001 and F-002 are copy, F-003 is a menu).

---

## As-Built Audit *(filled at Construction Review)*

## Ship Verify *(filled at `/pdlc ship` Verify step)*

---

## Revision History

| Date | Change | By |
|---|---|---|
| 2026-09-30 | Design-time audit (Step 10.6) and Variant Convergence skip (Step 10.7) | Muse, Jarvis |
