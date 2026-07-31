# UX Review — mobile-app
<!-- pdlc-template-version: 1.5.0 -->

**Triage:** Full
**Convened:** 2026-07-31
**Lead:** Muse (UX Designer)
**Participants:** Muse, Neo, Atlas, Friday, Bolt, Echo, Phantom, Pulse, Jarvis
**Status:** Pending human approval (Step 12)

---

## Triage Record

| Question | Answer | Evidence |
|---|---|---|
| Does this feature add or modify any user-facing UI surface? | **yes** | 8 new screens: LoginPage, DashboardPage, CalendarPage, CustomersPage, MessagingPage, NotificationsPage, AppointmentDetailPage, MessageThreadPage |
| Does this feature introduce a new flow, page, or significant interaction pattern? | **yes** | Provider login → dashboard → confirm flow; messaging inbox + thread; push notification UX; Shell tab bar navigation |
| Does this feature touch first-experience pathways? | **yes** | Login screen is the first-time entrypoint; post-login Dashboard has a first-time empty state; push notification permission request is a first-experience moment |

**Triage outcome:** Full

---

## Heuristics Scorecard (Nielsen 10)

| # | Heuristic | Score (0-4) | Severity if ≤2 | Notes |
|---|---|---|---|---|
| 1 | Visibility of system status | 3 | — | Loading states specified for API calls; push permission outcome needs explicit "enabled" confirmation |
| 2 | Match between system and real world | 4 | — | "Confirm", "Cancel", "Complete" match provider's mental model of appointment states |
| 3 | User control and freedom | 2 | P1 | No undo on irreversible status changes (Cancel, Complete); 401 redirect silently discards in-progress message body |
| 4 | Consistency and standards | 3 | — | Shell tab bar is platform-standard MAUI pattern; one open item: "Complete" vs "Done" — must be resolved |
| 5 | Error prevention | 3 | — | OwnershipGuard + status-transition guards prevent cross-account errors; Cancel/Complete should prompt confirmation |
| 6 | Recognition rather than recall | 3 | — | Tab labels + icons reduce recall; appointment cards must surface customer name + status + time without drill-in |
| 7 | Flexibility and efficiency of use | 3 | — | Mobile-first; swipe-to-dismiss notifications is a v2 enhancement; no regression here |
| 8 | Aesthetic and minimalist design | 3 | — | 7 screens appropriate for feature set; no superfluous fields |
| 9 | Help users recognize, diagnose, recover from errors | 2 | P1 | Error banners exist per PRD R10 but no retry action designed; error messages are generic ("Could not load appointments") |
| 10 | Help and documentation | 3 | — | Out of scope v1; provider domain familiarity makes help content unnecessary |

**Total: 29/40**   **Health band:** Good (28-35 revised band for mobile apps with no help/docs requirement)

---

## 8-State Coverage Matrix

| Element | Default | Hover | Focus | Active | Disabled | Loading | Error | Success |
|---|---|---|---|---|---|---|---|---|
| Sign In button | ✓ | n/a | ✓ | ✓ | ✓ (while loading) | ✓ | ✗ | ✓ |
| Confirm/Cancel/Complete CTA | ✓ | n/a | ✓ | ✓ | ✓ (after action) | ✓ | ✗ | ✓ |
| Send Message button | ✓ | n/a | ✓ | ✓ | ✓ (empty body) | ✓ | ✗ | ✓ |
| Mark Notification Read | ✓ | n/a | ✓ | ✓ | ✓ (already read) | ✓ | ✗ | ✓ |
| Calendar slot | ✓ | n/a | ✓ | ✓ | ✓ (booked) | ✓ | ✗ | n/a |

**Missing state: Error** — not designed at the interactive-element level for any primary CTA. Screen-level error banners exist (PRD R10) but individual button/element error states are absent.

---

## Cognitive Load Assessment

| # | Checklist item | Pass / Fail | Notes |
|---|---|---|---|
| 1 | Single primary action per screen | Pass | Each screen has one primary CTA |
| 2 | Progressive disclosure | Pass | Detail screens drill from list screens |
| 3 | Chunking (groups ≤ 7 items) | Pass | 5 tabs; appointment cards limited by date |
| 4 | Minimal mode switching | Pass | Shell navigation is flat and predictable |
| 5 | No jargon in primary flows | Pass | "Confirm", "Cancel" are plain language |
| 6 | Error messages are specific, not generic | Fail | "Could not load appointments" — no guidance on what to do (refresh? check network?) |
| 7 | Consistent label vocabulary | Conditional pass | "Complete" vs "Done" inconsistency flagged; must be resolved before build |
| 8 | Accessible labels on all interactive controls | Fail | PRD NFR mentions `SemanticProperties.Description` but no specific labels are designed; icon-only controls (send arrow, mark-read checkmark) lack `aria-label` equivalents |

**Failure count: 2 — Moderate**

---

## Anti-Patterns Found

| Pattern | Location | Severity | Proposed action |
|---|---|---|---|
| Modal-as-first-thought | Cancel/Complete confirmation — not specified in design | P1 | Replace with **bottom sheet** (native mobile confirmation pattern) |
| "Are you sure?" copy | Implied confirmation copy for Cancel/Complete | P1 | Replace with specific formula: name the action + consequence + action-specific buttons |
| Icon-only buttons without accessible labels | Send message (arrow icon), Mark read (checkmark icon) | P2 | Add `SemanticProperties.Description` on all icon-only interactive controls |

---

## UX Writing Findings

| Surface | Issue | Severity | Proposed copy |
|---|---|---|---|
| Error banners (all screens) | "Could not load [resource]" — no guidance on recovery | P1 | "Could not load appointments — check your connection and try again" [with Retry button] |
| Cancel confirmation | Generic "Are you sure?" assumed | P1 | "Cancel this appointment? [Customer name] will be notified. You can rebook anytime. / Cancel appointment / Keep it" |
| Complete confirmation | Generic assumed | P1 | "Mark this appointment as complete? This action can't be undone. / Mark complete / Go back" |
| Dashboard empty state | Not designed | P2 | "No appointments today — your next session is [date]. Check your calendar for upcoming slots." |
| Customers empty state | Not designed | P2 | "No customers yet — once a client books a session with you, they'll appear here." |
| Messages empty state | Not designed | P2 | "No messages yet — your conversations with clients will appear here." |
| Notifications empty state | Not designed | P2 | "You're all caught up — new booking updates will appear here." |
| Terminology | "Complete" used in PRD; must not appear as "Done" in UI | P2 | Use "Complete" throughout — matches PRD and `AppointmentStatus.Completed` enum value |

---

## Findings & Proposed Actions

### F-001 — No undo on irreversible actions + silent 401 redirect discards in-progress input

- **Source:** Heuristic-3 (User control and freedom)
- **Severity:** P1
- **Description:** Cancel and Complete are irreversible status transitions — there is no undo once committed. The design has no confirmation step for these actions, meaning a misfire (wrong appointment, wrong status) has no recovery. Additionally, the `JwtDelegatingHandler`'s 401 → navigate-to-login flow silently discards any in-progress user input (a message being composed, a note being reviewed). The provider loses their work with no warning.
- **Proposed action:** Fix now
  - Cancel/Complete CTAs must trigger a bottom sheet confirmation (native mobile pattern) before dispatching the API call. Copy per UX Writing Findings above.
  - The 401 redirect must save the in-progress state (or warn the user before clearing) before navigating to login. At minimum: "Your session expired. Your in-progress message was not sent — please sign in and try again."
  - Both will land as Plan-phase Beads tasks.
- **Decision (human, at Step 12 approval):** *[pending]*
- **Cross-talk note:** Phantom's T-NL-2 flagged the 401 silent-discard from a repudiation angle. Muse escalated it to P1 because the user-impact is loss of in-progress work, not just an audit-trail gap.

---

### F-002 — Error states have no retry mechanism

- **Source:** Heuristic-9; Cognitive-load item 6; UX Writing
- **Severity:** P1
- **Description:** PRD R10 requires a "visible error state" when API calls fail. The design describes a "banner or inline message" but neither includes a retry action. A provider who opens the Dashboard to an offline backend sees "Could not load appointments" with no way to retry without closing and reopening the app. On a flaky mobile network (between sessions) this is the most common failure mode.
- **Proposed action:** Fix now
  - Every screen-level error banner must include a "Try again" button that re-triggers the ViewModel's load command.
  - Error message copy must follow the formula: what happened + why + how to fix (per UX Writing Findings).
  - Will land as a Plan-phase Beads task.
- **Decision (human, at Step 12 approval):** *[pending]*

---

### F-003 — Error state not designed at the interactive-element level

- **Source:** 8-state coverage matrix — Error state missing on all primary CTAs
- **Severity:** P2
- **Description:** All five primary interactive elements are missing their Error state. When "Sign In" fails (wrong credentials, network down), the button itself has no visual signal that the action failed — the error is only at the screen level (a banner). For "Send Message", there is no visual indication on the button that the send failed. Users learn to look at the button for immediate feedback; an error banner that appears elsewhere on the screen is easy to miss.
- **Proposed action:** Mitigate later
  - Error state design for individual controls should be addressed before Construction Review. It is not a blocker for design approval but must not reach ship without at minimum the Sign In button having a clear error state (red border, shake animation, or inline error beneath the field).
  - ADR in DECISIONS.md — accepted as design-time gap, to be addressed in construction.
- **Decision (human, at Step 12 approval):** *[pending]*

---

### F-004 — Accessible labels not specified for icon-only controls

- **Source:** Cognitive-load item 8; PRD NFR (accessibility); Anti-pattern scan
- **Severity:** P2
- **Description:** PRD NFR requires `SemanticProperties.Description` on all interactive controls. The design mentions this but specifies no actual label values. Icon-only buttons (send arrow, mark-read checkmark, notification dismiss) without specified accessible labels will either be left blank (screen reader says "button") or given inconsistent labels by implementers.
- **Proposed action:** Mitigate later
  - The Plan-phase MAUI project setup task should include an accessibility label specification as a required subtask. Before merge, Echo runs an accessibility sweep that catches any `SemanticProperties.Description` absence on interactive controls.
  - ADR in DECISIONS.md.
- **Decision (human, at Step 12 approval):** *[pending]*

---

### F-005 — Cancel/Complete confirmation must use bottom sheet, not modal

- **Source:** Anti-pattern scan — modal-as-first-thought
- **Severity:** P1
- **Description:** On mobile, confirmation actions are native-patterned as bottom sheets (`ActionSheet` in MAUI or a custom `BottomSheet`), not blocking modals. A confirmation modal breaks platform conventions on both iOS and Android — it doesn't feel native, it covers more screen than necessary, and it's harder to dismiss accidentally. The MAUI `Shell.DisplayAlert` equivalent (a system-level alert) is too severe for "are you sure?" — it resembles an OS-level error. Bottom sheet is the right pattern.
- **Proposed action:** Fix now
  - Cancel/Complete confirmation must be implemented as a `ActionSheet` (MAUI) or a custom bottom sheet if more copy control is needed. The copy formula is specified in UX Writing Findings.
  - Will land as a Plan-phase Beads task (MAUI confirmation bottom sheet component).
- **Decision (human, at Step 12 approval):** *[pending]*
- **Cross-talk note:** Atlas confirmed this matches the provider persona's mobile expectations — they're on their phone between sessions, not at a desktop. Phantom noted that the bottom sheet pattern also avoids the security-required confirmation concern from T-003, since the explicit CTA name ("Cancel appointment" vs "Keep it") is unambiguous.

---

### F-006 — Empty states not designed for any list screen

- **Source:** UX Writing — empty states absent; first-experience pathway
- **Severity:** P2
- **Description:** Four screens (Dashboard, Customers, Messages, Notifications) have no empty-state design. A new provider logging in for the first time sees four blank screens. Empty states are first-experience pathways — they're the onboarding moment. A blank screen with no guidance is a dead end. The copy in UX Writing Findings provides ready-to-implement empty-state text for all four screens.
- **Proposed action:** Mitigate later
  - Empty states should be addressed in the MAUI build before Construction Review; they don't block design approval. Copy is specified above.
  - ADR in DECISIONS.md.
- **Decision (human, at Step 12 approval):** *[pending]*

---

## Open Questions for Human

1. **Cancel/Complete confirmation — bottom sheet vs. system alert trade-off:** The MAUI `ActionSheet` (system alert on iOS) is the fastest-to-implement confirmation, but it uses platform-system styling that can feel jarring for app-branded confirmation flows. A custom bottom sheet gives copy control but adds a component to build. Which do you prefer: system `ActionSheet` (fast, standard, less control) or a custom bottom sheet (slower, more control over copy/styling)?

2. **Push notification permission prompt timing:** iOS requires an explicit permission prompt for push notifications. Should the app request push permission immediately at first login (maximizes opt-in, but may feel pushy before the user has experienced value) or after the first appointment event fires (contextual, but risks the user missing their first notification)? This decision affects the FCM registration flow design.

---

## Variant Convergence *(Step 10.7)*

**Outcome: Skipped** — Step 10.6 Full triage produced 0 P0 findings, H3 and H9 scored 2 but neither is on a major surface where visual variants would resolve them (both are behavioral/copy fixes, not layout variations). No trigger signals fired.

---

## Approval Outcomes *(filled in at Step 12)*

| Finding ID | Muse's recommendation | Human decision | Rationale |
|---|---|---|---|
| F-001 | Fix now | *[pending]* | — |
| F-002 | Fix now | *[pending]* | — |
| F-003 | Mitigate later | *[pending]* | — |
| F-004 | Mitigate later | *[pending]* | — |
| F-005 | Fix now | *[pending]* | — |
| F-006 | Mitigate later | *[pending]* | — |

**ADR registry updates required (after human approval):**
- ADR for F-003 (error state at element level — design-time gap accepted, to be addressed in construction)
- ADR for F-004 (accessible labels deferred to construction task)
- ADR for F-006 (empty states deferred to construction)

**Beads tasks to be created at Plan (Step 13):**
- F-001 fix: Cancel/Complete bottom-sheet confirmation + 401-redirect in-progress input warning
- F-002 fix: Error banner retry button + specific copy on all list screens
- F-005 fix: MAUI bottom sheet confirmation component for irreversible status actions

---

## Revision History

| Date | Author | Change |
|---|---|---|
| 2026-07-31 | Muse (initial draft) | Created at Step 10.6 — Full Roundtable (3/3 triage gates) |
