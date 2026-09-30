---
feature: provider-showcase
date: 2026-09-30
status: design-approved
last-updated: 2026-09-30T17:17:07Z
approved-by: ogdevlabs
approved-date: 2026-09-30T17:17:07Z
prd: docs/pdlc/prds/PRD_F-037_provider-showcase_2026-09-30.md
---

# Brainstorm Log: Provider Showcase

> Seed request (verbatim): "provider rich content capabilities, enable provider to upload his picture, logo, a collection of images 20 max to present his/her work, ability to generate QR code so they can publish on their social networks and get customers redirected to their profile and services, build this like a UX marketing immersive offering, engage heavily Agent Muse"

## Divergent Ideation

**Technique:** AI-recommended — Structured Domain Rotation + Role Storming (the IG-story scanner, the skeptical customer)
**Total ideas generated:** 102
**Completed:** 2026-09-30

### Raw Ideas (condensed)
Technical: 1 private Azure Blob + MI; 2 GridFS; 3 client downscale 1600px; 4 server renditions (ImageSharp); 5 SAS direct upload; 6 separate ShowcaseEntity; 7 vanity slug; 8 opaque public id; 9 anonymous PII-minimised public GET; 10 client-side QR.
UX: 11 Showcase hub on Profile; 12 view-as-customer preview; 13 full-bleed hero + logo badge; 14 3-col grid + immersive swipe; 15 captions tagged to services; 16 drag reorder/cover; 17 completeness meter; 18 pinned Book CTA; 19 Subscribe/Book/Message replacing alert; 20 bilingual share copy.
Business: 21 QR as acquisition engine; 22 branded QR card exports; 23 ?src attribution; 24 free/premium tiers; 25 referral credit; 26 SEO landing page; 27 completeness ranking; 28 WhatsApp-first; 29 printable cards; 30 scan→booking funnel.
Edge: 31 moderation; 32 EXIF GPS strip; 33 third-party faces consent; 34 no-app dead end; 35 deactivated/erased link; 36 slug change breaks printed QR; 37 flaky uploads; 38 atomic 20-cap; 39 scraping/enumeration; 40 storage cost/orphans.
Technical II: 41 Universal/App Links; 42 server-rendered HTML fallback; 43 OG tags; 44 deferred deep link; 45 CDN; 46 content-addressed blobs; 47 media route via Gateway; 48 blurhash; 49 mobile image cache; 50 magic-byte/size/decode-bomb validation.
UX II: 51 onboarding step; 52 story-mode; 53 parallax hero; 54 service cards w/ Book; 55 accent from logo; 56 recently viewed; 57 native share sheet; 58 logo-centre QR; 59 profession empty states; 60 alt-text/VoiceOver.
Business II: 61 portfolio-driven professions first; 62 link-in-bio; 63 promo banner; 64 reviews; 65 verified-payouts badge; 66 next-free-slot teaser; 67 per-campaign QRs; 68 PDF media kit; 69 C2C share; 70 ToS licence grant.
Edge II: 71 trademark logos; 72 blocked customer sees public page; 73 require verified email; 74 teaser leaks busy-ness; 75 HEIC; 76 offline queue; 77 3G progressive; 78 bio language; 79 erasure deletes blobs/CDN; 80 gone page.
Analogies: 81 Linktree; 82 Airbnb; 83 Behance; 84 restaurant menu QR; 85 Spotify Codes; 86 IG link sticker; 87 Calendly; 88 Uber Eats storefront; 89 Wallapop auto-enhance; 90 skeptical customer wants proof.
Wild: 91 time-aware QR; 92 auto reel; 93 one-tap rebook; 94 NFC; 95 AI captions; 96 UGC portfolio; 97 showcase of the week; 98 QR in confirmation email; 99 photo becomes avatar (closes 5qr); 100 profession theme packs; 101 vCard QR; 102 provider scans customer QR.

### Clusters
Storage & media pipeline: #1,#2,#3,#4,#5,#45,#46,#47,#48,#50,#75,#77
Public identity & reachability: #7,#8,#9,#34,#36,#39,#41,#42,#43,#44,#80,#84,#87
Immersive showcase UX: #11,#12,#13,#14,#15,#16,#52,#53,#54,#55,#60,#82,#88
Growth & sharing: #10,#20,#21,#22,#23,#28,#57,#58,#62,#85,#86,#98
Trust, safety & compliance: #31,#32,#33,#35,#38,#40,#70,#71,#73,#79
Later / monetisation: #24,#25,#63,#64,#67,#68,#92,#95,#96,#100
[overlaps deferred agenda-buddy-5qr]: #99

### Standouts
1. #8 Opaque immutable public id — printed QRs never break
2. #34/#42/#84 No-install web fallback page
3. #41 Universal/App Links
4. #6 Separate ShowcaseEntity (whole-document PUT protection)
5. #3+#50+#32 Client downscale + server validation + EXIF strip
6. #1/#5 Private Blob storage with managed identity
7. #13/#53 Full-bleed hero + logo badge reusing Profile hero
8. #15/#54 Portfolio→service link with inline Book
9. #18/#19 Real provider detail page with pinned actions
10. #22/#58 Branded QR card exports
11. #23/#30 Source attribution funnel
12. #99 Photo becomes avatar — closes 5qr
13. #35/#79/#80 Erasure deletes blobs; friendly gone page
14. #31/#33/#70 Report + consent attestation + ToS licence
15. #28 WhatsApp-first sharing

## Socratic Discovery

**Interaction mode:** Sketch

### Round 1 — Problem Statement (accepted as proposed)

**Q1:** What problem does this feature solve?
**A:** Providers cannot present themselves or their work in AgendaMe, and cannot direct anyone to it. Customers see only name/professions/services in a directory list; "sessions" is an alert (CustomersViewModel.ShowSessionsAsync:286). No photo, logo, portfolio or detail page exists, and every provider route requires a JWT, so a link posted on social media has nowhere to land. Portfolio-driven providers (tattoo artists, stylists, trainers) keep marketing on Instagram/Linktree; AgendaMe gains no acquisition from its own providers.

**Q2:** Who uses it, and in what context?
**A:** (1) Provider — portfolio-driven sub-group of the INTENT persona; sets up once from Profile (~5 min), shares QR/link from IG bio/stories, WhatsApp status, printed studio card. (2) Prospective customer — new, usually signed out, scanning/tapping from social media on a phone that likely lacks the app. Signed-in directory browsing is secondary. Muse: design for the no-app visitor first.

**Q3:** What does success look like?
**A:** (a) ≥60% of active providers have a showcase with photo + ≥3 portfolio images within 30 days; (b) QR/link visits attributed (?src=qr|link), ≥20% of visits produce a signup-or-book intent tap; (c) new customer QR scan → booked first appointment < 3 minutes. Measurement (attribution) is IN scope.

**Q4:** Technical constraints / dependencies — one PRD or several?
**A:** Constraints: MongoDB.Driver 2.25 pin; no blob storage exists; new packages need discussion (QR encoder, possibly image processing, blob SDK); Gateway explicit allowlist; email must never appear in a public URL (T-004); whole-document provider PUT (2wf) → targeted $set routes or own collection; AccountErasure must delete blobs; es-MX for every string; no custom domain on gateway. Decision: ONE feature in 3 waves — W1 storage + photo/logo/gallery + in-app showcase (closes 5qr, photo→avatar); W2 public id + public read API + minimal web landing + QR/share; W3 Universal/App Links + OG unfurl + attribution.

### Round 2 — Future State / Key Capabilities

**Q1:** What does a provider author?
**A (accepted):** "My Showcase" hub from Profile (provider-only row). Profile photo (camera/library, square crop) becomes the avatar everywhere AvatarSource resolves one — closes agenda-buddy-5qr; catalogue marks remain fallback. Optional logo (hero badge + QR centre). Tagline ≤80, About ≤600. Portfolio 0–20 images, optional caption ≤140, optional link to one of the provider's services, drag-reorder, first = cover. Client downscale 1600px long edge JPEG ~85%; server accepts JPEG/PNG/WebP ≤5 MB, magic-byte validation, EXIF/GPS strip, atomic 20-cap.

**Q2:** Where do images live?
**A (accepted):** Private Azure Blob container per environment, accessed only by the Provider service via managed identity; served through a new authenticated Gateway route `/api/v1/media/{contentHash}` with immutable cache headers; SHA-256 content-addressed names. Azurite via Aspire locally. Packages to approve: Azure.Storage.Blobs, Aspire.Hosting.Azure.Storage.

**Q3:** How does a signed-out visitor with no app reach the showcase?
**A (USER OVERRIDE):** They must download the app and register before any provider content is visible. **No external, outside-the-app features**: no web landing page, no anonymous public API, no OG unfurl, no custom domain, no Universal/App Links. Follow-up decision — QR target: **Store link + in-app scan.** The QR encodes a store link carrying the provider's opaque public id; after install + registration the customer scans the same QR (or types the short code) with an in-app scanner, which opens the provider's showcase. Opaque public id retained (email never in a QR). Attribution becomes in-app (recorded when a signed-in user opens a showcase via scan/code/link).

**Q4:** What does the customer see in the app, and how does sharing work?
**A (accepted, adjusted for Q3):** New ProviderShowcasePage (route providerShowcase?publicId=): full-bleed hero, logo badge, tagline, portfolio grid → full-screen swipe viewer, service cards, pinned Book / Subscribe / Message. Reached from a directory row tap (replacing the ShowSessions alert), the in-app QR scanner / code entry, and "Preview as customer". QR & Share screen: QR generated on-device (pure-C# encoder, package approval), high error correction with logo centre; exports Story 1080×1920, Square 1080×1080, Printable card; share sheet WhatsApp-first. (Universal/App-link and signed-out return-to entry points dropped by Q3.)

### Round 3 — Acceptance Criteria

**Q1:** What does the QR encode, given iOS and Android need different stores?
**A (accepted):** One QR per provider → `https://<gateway>/api/v1/go/{code}`, an anonymous content-free 302 that routes by User-Agent (iOS → App Store, else → Play). Returns no provider data; the only anonymous route in the feature. Precedent: `PaymentWebhookModule.cs:16-26`. The in-app scanner parses `{code}` from the same URL. The 6-char code is printed beneath the QR as the manual fallback.

**Q2:** Provider-side done?
**A (accepted):** (a) Complete showcase (photo, logo, tagline, About, 1–20 captioned images) buildable in-app. (b) Photo, logo, portfolio each persist via their own targeted write — never the whole-document profile PUT. (c) 21st image refused with a reason. (d) Account erasure deletes every stored blob. (e) Preview-as-customer renders exactly the customer view.

**Q3:** Customer-side done?
**A (accepted):** (a) Registered customer reaches a showcase from Home in ≤2 taps via scan or code. (b) Book / Subscribe / Message pinned. (c) Unknown, deactivated or erased code → explicit not-found state, never blank. (d) Zero provider content (images, names) is reachable before sign-in.

**Q4:** Success metrics and out of scope?
**A (accepted):** Each scan/code open recorded as {provider, source ∈ scan|code|directory}; provider hub shows "Visits from your QR"; north-star = first booking within 7 days of a scan. Out of scope: video, filters/editing beyond crop, public web profile, custom domain, reviews/ratings, code re-issue. EN + es-MX from day one.

## Progressive Thinking (Agent Team Meeting)

**MOM:** [provider-showcase_progressive-thinking_mom_2026_09_30.md](../mom/provider-showcase_progressive-thinking_mom_2026_09_30.md)

### Confirmed Facts
No blob storage, multipart, anonymous provider route, provider detail page, media picker, camera permission, or QR/scan package exists. Showcase writes must be targeted (2wf / ADR-032). Gateway allowlist needs `/media` + `/go` rows. T-004 forbids email in URLs. Erasure must cover everything. INTENT.md:63 exclusion must be lifted by ADR.

### Accepted Inferences
Separate `provider_showcase` collection; two-step upload (bytes→hash, then metadata); non-sequential 6-char code (31-symbol alphabet); content-free `/go`; ZXing.Net.Maui (scan) + QRCoder (generate) pending approval; Azurite local / managed identity cloud; stream-based authenticated image loader with hash-keyed disk cache.

### Key Consequences
7 new mobile views + rewired directory tap + Home first-run card; Provider.Core handlers + media + `/go` modules; storage infra + unconditional Cloud parameter; privacy policy update; 2 ADRs + INTENT; Bruno `9-Showcase`; OpenAPI regen via the baseline writer.

### Risks & Unknowns
UGC moderation (App Review 1.2 → report action); store URLs unknown (1hk.14 → config); orphan blobs; unknown-UA default chooser; per-image upload retry; server image library license.

### Conflicts Resolved
1. Deferred deep linking rejected (privacy) → printed code + Home first-run card. 2. Export uses existing graphics stack, no new package. 3. Server image library → escalated at package approval.

### Design Priorities
1. Upload safety pipeline (re-encode everything). 2. Content-free anonymous `/go`. 3. Customer's first 10 seconds (Muse). 4. Targeted atomic writes + complete erasure. 5. Authenticated client image loader.

## Adversarial Review

**Completed:** 2026-09-30T16:44:02Z

### Findings
1. UGC compliance gap — App Review 1.2 requires filter/report/block/contact for user-generated content; a portfolio is UGC.
2. Global content-hash dedup breaks erasure — two providers sharing one blob; erasing one breaks the other.
3. Funnel invisible — scans that never install are uncounted; north-star has no denominator.
4. "Visits from your QR" gameable — self-scans and repeat scans inflate it.
5. Store URLs don't exist (1hk.14) — printed QR is permanent; targets must be configuration.
6. No thumbnails — 20 × 1600px grid ≈ 8 MB decoded; janky on low-end Android.
7. Erasure does not reach client caches holding "immutable" images.
8. Five new packages vs CONSTITUTION §9; ZXing.Net.Maui on .NET 10 MAUI unverified.
9. Photo-as-avatar touches every avatar render site; N authenticated fetches per list.
10. Scanner entry point on Home undesigned; Dashboard differs by role.
11. Deactivated provider's own Preview behaviour undefined.
12. Sizing — storage infra + pipeline + 7 views + scanner + QR + attribution exceeds recent features.
13. `/go` rate limiting defaults off locally (ADR-033); abuse tests must enable it.
14. Rendered share assets need es-MX text; localisation AC didn't cover images.

### Follow-up Q&A
**Q (F1):** How is UGC compliance met?
**A (accepted):** Report action on every showcase (reasons: inappropriate / not their work / spam / other) → stored `showcase_report` + operator email; reporting customer can "Hide this provider" (block, removes from their directory and scan results); Terms gain provider-content clause + contact address; operator review manual for now.

**Q (F2):** Dedup vs erasure, thumbnails, cache?
**A (accepted):** Blob key `{providerId}/{sha256}/{variant}` — dedup per provider only; erasure deletes the prefix. Server writes `full` (1600px) + `thumb` (480px) per upload; grid loads thumb. Client cache TTL 30 days (not forever), evicted when a provider's showcase returns not-found.

**Q (F3):** How is the funnel made measurable?
**A (accepted):** `/go/{code}` increments an anonymous per-code, per-platform (ios/android/other) counter — no IP, no UA stored. Hub shows Scans → Opened in app → Booked. Provider's own visits and same-customer repeats within 24h excluded.

## Edge Case Analysis

**Completed:** 2026-09-30T16:45:53Z

### Findings

| # | Category | Scenario | Trigger Condition | Addressed? | Risk if Unhandled |
|---|----------|----------|------------------|------------|-------------------|
| 1 | Flow branch | Provider leaves portfolio editor mid-upload | Back-nav with uploads in flight | No | Orphan blobs; provider believes it saved |
| 2 | Concurrency | Two devices add images at 19/20 | Simultaneous `$push` near cap | Partial | Unclear 409, undefined order |
| 3 | Concurrency | Reorder from stale list after other-device delete | Stale hash set on PUT | No | Resurrected image or silent failure |
| 4 | Boundary | Exactly 5 MB / 20000×20000 px under 5 MB | Byte vs pixel cap | Partial | Decompression bomb |
| 5 | Invalid input | Animated GIF/WebP, HEIC, CMYK JPEG | iOS camera default HEIC | No | Most common iPhone photo rejected |
| 6 | Invalid input | Emoji/RTL in captions; char vs byte counting | Unicode | No | Mid-emoji truncation; client/server disagree |
| 7 | Permission | Customer JWT on provider write routes | Role/ownership mismatch | Partial | Showcase takeover |
| 8 | Permission | Blocked customer / hidden provider via scan or code | Scan/code/directory | Partial | Block not enforced on scan |
| 9 | Permission | Camera permission denied/revoked | OS settings | No | Black scanner screen |
| 10 | Integration | Blob store down on upload/fetch | Storage outage | No | Half/blank showcase |
| 11 | Integration | Non-AgendaMe QR scanned in-app | Menu QR | No | Crash or silent no-op |
| 12 | Scale | 20 images on slow 3G | Poor network | Partial | Hub unusable |
| 13 | Partial completion | Blob written, attach fails | `$push` fails after blob write | No | Orphan + duplicate on retry |
| 14 | Partial completion | Erasure deletes doc, blob delete fails midway | Storage outage | No | Erased account's images survive |
| 15 | Migration | Existing providers lack `public_code` | Every current provider | No | QR & Share broken for all existing providers |
| 16 | Migration | Mixed app/backend versions during rollout | Staggered release | No | Blank avatar |
| 17 | Flow branch | Photo replaced; old blob and caches remain | Photo change | No | Stale face 30 days; storage leak |
| 18 | Boundary | Portfolio item linked to deleted service | Service removed | No | Dead tap |

### Triage Decisions

| # | Decision | Notes |
|---|----------|-------|
| 1 | In scope | In-flight uploads continue per-item; leaving shows "N uploads still in progress — leave anyway?"; unattached blobs swept (E1) |
| 2 | In scope | Cap enforced by `$size` guard; the loser gets 409 "Your portfolio is full (20 of 20)" |
| 3 | In scope | Reorder PUT carries the full hash list; server rejects (409) if it is not a permutation of the current set; client reloads |
| 4 | In scope | Header-only probe before full decode: >5 MB, >40 MP, or side >8000 px → 400 with reason (E2) |
| 5 | In scope | Client converts HEIC/any to JPEG before upload; server accepts JPEG/PNG/WebP, first frame only, sRGB-normalised (E2) |
| 6 | Out of scope (partial) | Lengths counted in grapheme clusters on both sides; any script accepted; no RTL-specific layout |
| 7 | In scope | All showcase writes owner-guarded on the JWT `sub` + provider role; 403 otherwise |
| 8 | In scope | Hide/block honoured in directory, scan and code resolution (resolves to not-found for that customer) |
| 9 | In scope | Denied camera → explanatory state with "Open Settings" + "Type a code instead" |
| 10 | Known risk | Best-effort: placeholder tiles, per-image retry; no offline mode |
| 11 | In scope | Non-AgendaMe QR → inline "That isn't an AgendaMe code" and scanner stays open |
| 12 | Known risk | Thumbnails + progressive load mitigate; no offline mode |
| 13 | In scope | `media_ref` recorded at blob write; unattached >24h swept hourly by Provider `BackgroundService`; re-upload of same bytes idempotent (same hash) |
| 14 | In scope | Erasure deletes doc first then prefix; blob failures finished by the sweep; route still 204 |
| 15 | In scope | `public_code` minted lazily on first QR & Share / showcase read, unique index, collision retry |
| 16 | Out of scope | Store-gated client release; backend additive + nullable |
| 17 | In scope | Replacing photo/logo detaches old ref (swept); client cache keyed by hash so new photo shows immediately |
| 18 | In scope | Service link rendered only if the service still exists; dangling link ignored, not an error |

### Follow-up Q&A
**Q (E1):** Orphan and partial-delete recovery?
**A (accepted):** `media_ref {providerId, hash, attachedAt?}` on every blob write; hourly in-process `BackgroundService` sweeps unattached >24h and erased-provider refs; erasure doc-first then prefix, sweep completes failures, 204 preserved.

**Q (E2):** Accepted formats?
**A (accepted):** Client always converts to JPEG (incl. HEIC). Server JPEG/PNG/WebP only, first frame, sRGB; refuses >5 MB, >40 MP or side >8000 px before full decode; 400 with reason.

## UX Discovery

**Completed:** 2026-09-30T17:03:09Z
**Lead:** Muse (UX Designer)
**Visual companion:** active — fragments at `.pdlc/brainstorm/provider-showcase-44879-1790785256/content`

### UI inventory (grounding)

Established UX patterns:
- Full-bleed `BrandGradient` hero leading the screen (ProfilePage)
- `BrandHeader` first element on every non-auth view; back `‹` derived from nav stack (BackNavigationTest)
- Named actions pinned outside scrollers (PinnedActionTest); destructive actions last and outside settings cards (ProfilePage)
- Flat-list-with-headers over grouped CollectionView (professions, notifications)
- Distinct "no longer available" vs "could not load" messages (AppointmentDetailPage)
- In-app snackbar via `IInAppAlertService`

Component library: `Controls/Avatar`, `BrandHeader`, `SafeButton`, `SlotPicker`, `LegalDocumentBody`; CommunityToolkit.Maui 9.1.1.

Design tokens (App.xaml): Primary #087F6A family, Accent #D79B2B family, BrandGradientStart/Mid/End, BackgroundPage/Card, TextPrimary/Secondary/Muted, Success/Error/Warning/Info + Light variants.

Recent UX decisions: ADR-064 abstract avatars (no faces); agenda-buddy-po0 double header accepted; no CanExecute-gated submit on XAML-bound state.

Greenfield flag: no

### Q1 — Look and feel
**Options presented:**
- Option A: Magazine hero (cover in ProfilePage BrandGradient hero slot, avatar overlap, logo badge) + 3-col portfolio grid + service cards + pinned Book/Subscribe/Message — ProfilePage + PinnedActionTest patterns
- Option B: Story-style full-screen tap-through portfolio — new pattern
- Option C: Services-first with work strip — ServicesPage pattern

**Selected:** Option A
**Visual companion fragment:** `.pdlc/brainstorm/provider-showcase-44879-1790785256/content/ux-discovery-q1-q3.html`

### Q2 — Flow
**Options presented:**
- Option A: Dashboard first-run card → ScanProviderPage (camera + "Type a code instead") → ProviderShowcasePage → existing BookAppointmentPage (2 taps); card collapses to a "Scan a code" row in More after first subscribe/booking
- Option B: Scan icon in BrandHeader everywhere
- Option C: "Add provider" inside the providers list (3 taps)

**Selected:** Option A. Provider side fixed: Profile → My Showcase hub (completeness meter) → Photo · Logo · Tagline & About · Portfolio · Preview as customer · QR & Share.

### Q2b — Returning / cold-feet customer (raised by user)
**User question:** "after a customer make a choice to book services, and have second thoughts or cold feet, how existing subscribed customer would explore provider current gallery"
**Accepted (Muse):** Rule — wherever the provider's name or avatar appears, one tap opens their work.
1. AppointmentDetailPage: provider "See {FirstName}'s work" chip + 4-image strip, placed above Reschedule/Cancel.
2. Showcase is relationship-aware: "Your next session" banner (Details / Reschedule) replaces the Book hero; "Book again"; Subscribed ✓; NEW tag on items added since the customer's last visit (derived from `showcase_visits`).
3. Providers list row tap → showcase (replacing `ShowSessionsAsync` alert) with "N NEW" chip.
4. MessageThreadPage header avatar/name → showcase.
5. BookAppointmentPage: 4-image strip at top, reassurance before confirming.
Deferred: "new work added" push (marketing message — needs opt-in + new NotificationType).
**Fragment:** `.pdlc/brainstorm/provider-showcase-44879-1790785256/content/returning-customer-v1.html`

### Q3 — State coverage
**Selected (as shown):** Empty = cover falls back photo → brand gradient + avatar mark, services still shown; hub shows "Add your first work" CTA. Loading = skeleton hero/tiles, cover thumb first, tiles fade in. Error = "This provider isn't available" + "Scan another code" (not found) vs "Couldn't load — Try again" (network). Success = full showcase; one-time "Opened from {FirstName}'s code" snackbar after scan. Scanner: camera denied → Open Settings / Type a code instead; foreign QR → inline "That isn't an AgendaMe code", scanner stays open.
**Visual companion fragment:** `.pdlc/brainstorm/provider-showcase-44879-1790785256/content/ux-discovery-q1-q3.html`

### Quality Pass
**Cognitive-load assessment:** 1/8 failures — acceptable
- Item 2 (groups ≤4): My Showcase hub has 6 entries → mitigated by grouping into 3 cards: *You* (Photo, Logo, Tagline & About), *Your work* (Portfolio), *Share* (Preview as customer, QR & Share).

**Personas selected:** first-time user + distracted-mobile user + accessibility-dependent user
**Persona red-flag findings:**
- Distracted-mobile: upload interrupted by connection drop — handled per-item with retry (edge case 1/13); P2, non-blocking.
- Accessibility-dependent: camera scanning unusable for low-vision users — "Type a code instead" on the scanner and the printed code; P2, non-blocking. Portfolio images need caption-as-description (placeholder label when uncaptioned); P2.
- First-time user: arrives via store with no memory of which provider — Dashboard card explains "Found a provider? Scan their code"; P3.

**Decision Review triggered:** no

### Design Deviations
No deviations — every selection composes existing components and patterns. (Option B story viewer was the only new pattern and was not selected; the full-screen portfolio *viewer* reached from a grid tile is a standard swipe pager.)

## Design Discovery (Bloom's Taxonomy)

Sketch mode, all three rounds batched; accepted as proposed by ogdevlabs, 2026-09-30. Built on UX Discovery Q2-A (Dashboard card → scanner → showcase → booking).

### Round 1 — Mechanics
- **Q1 Placement:** everything lives in the Provider service: `ShowcaseModule`, `MediaModule` and `GoModule` in `AgendaBuddy.Provider.Api`, with handlers in `AgendaBuddy.Provider.Core` that audit via `eventStore.SaveAsync`. The Gateway gets new allowlist rows pointing to the provider cluster. No new process, no new `deployable` entry.
- **Q2 Consistency:** `POST /media` validates, re-encodes, writes both variants, then inserts a `media_refs` record with `attached=false`, and only then returns the hash. The attach command runs a `$push` guarded by `$size` and marks the record attached. A crash in between leaves an orphan that the hourly sweep collects after 24 hours. Customers read only the showcase document, so they never see a half-written state.
- **Q3 First use vs return:** the showcase document is created on the first authoring write (insert-if-absent plus a duplicate-key catch; `FindOneAndUpdateAsync` never upserts, ADR-032). The public code is created on first use with a collision retry. NEW tags are computed as `added_at` > the customer's `showcase_visits.last_seen_at`.

### Round 2 — Apply
- **Q4 Blob layer:** an `IBlobStore` in Library, implemented by `AzureBlobStore` (`Azure.Storage.Blobs` + `DefaultAzureCredential`) and by `InMemoryBlobStore` for tests. Locally, Azurite via `Aspire.Hosting.Azure.Storage` `RunAsEmulator()`. The Cloud-shape storage resource is declared unconditionally, and the role is assigned to the provider app only.
- **Q5 Image library:** SkiaSharp (MIT) plus `SkiaSharp.NativeAssets.Linux.NoDependencies`. ImageSharp is rejected (Split License); Magick.NET is the fallback if WebP decoding fails in the container. Packages to approve: `Azure.Storage.Blobs`, `Azure.Identity`, `SkiaSharp` (+ native assets), `QRCoder`, `Aspire.Hosting.Azure.Storage`, `ZXing.Net.Maui`.
- **Q6 Patterns:**
  - Carter modules, `OwnershipGuard`, and targeted `FindOneAndUpdateAsync` writes.
  - A sweep modelled on `AppointmentAutoCompletionService`, and a `/go` limiter mirroring Identity's config-gated limiter (ADR-033).
  - `AccountErasure` extended inside the single `AddAccountErasure` set.
  - Mobile: `ShowcaseRouteBuilder`, an `AuthenticatedImageSource` (stream plus bearer token, with a hash-keyed disk cache), and `Share.RequestAsync`.
- **Spike result:** SkiaSharp is **not** transitive in `AgendaBuddy.MobileApp` (0 hits in `project.assets.json`), so share-image export uses `Microsoft.Maui.Graphics`.

### Round 3 — Trade-offs and Judgments
- **Q7 Storage down:** fail closed per operation. Upload and media GET return 503; the client shows per-tile retry or a cached copy; the showcase text and services still render; `/go` is unaffected. Storage is **not** part of `/health` readiness.
- **Q8 Media authorization:** any signed-in user may fetch media (accepted risk). The content is promotional, hashes are 256-bit, and relationship checks would break Preview and the scan-first flow.
- **Q9 Scope:** one PRD; Plan's split check decides. Expected outcome: F-037a (W1) / F-037b (W2 + W3).

### Synthesis
- **Components:** Provider.Api (+3 modules), Provider.Core (~12 handlers), Library (`ShowcaseService`, `MediaService`, `IBlobStore`, `ImagePipeline`, `PublicCodeGenerator`, `MediaSweepService`), Gateway (+3 rows), AppHost (+storage), MobileApp (7 views, image loader, `IQrScanner`).
- **Collections:** `provider_showcase`, `media_refs`, `showcase_visits`, `go_counters`, `showcase_reports`, `showcase_blocks`.
- **ADRs:** ADR-069 (image storage and serving; lifts INTENT.md:63), ADR-070 (sign-in-only showcase + content-free `/go`), ADR-071 (collections + erasure extension).

Validated without pushback.

## Threat Modeling Triage
- Trust boundary changes: yes — anonymous `/go` route; blob egress; user binary content entering the service
- Regulated data: yes — face photos, EXIF/GPS in raw uploads, visit records
- New attack surface: yes — image parser, redirect, 14 routes, lookup, in-app scanner, user-generated content
- Triage tier: Full
- Output: `docs/pdlc/design/provider-showcase/threat-model.md` (T-371..T-384, T-NL-1..6); MOM `docs/pdlc/mom/MOM_threat-model_provider-showcase_2026-09-30.md`

## Design-Laws Audit Triage
- UI surface: yes — 7 new views; chips on AppointmentDetail, BookAppointment, MessageThread; providers-list tap
- New flow / pattern: yes — scan → showcase → book; reorder grid; swipe viewer; share-image export
- First-experience pathway: yes — QR install path, Dashboard first-run card, empty showcase/hub
- Triage tier: Full
- Output: `docs/pdlc/design/provider-showcase/ux-review.md` — Nielsen 28/40 (Good), audit 14/20 (Good), cognitive load 1/8; 3 P1 + 7 P2, all fix-now; MOM `docs/pdlc/mom/MOM_design-laws_provider-showcase_2026-09-30.md`

## Variant Convergence Triage (Step 10.7)

- Step 10.6 triage tier: Full
- Trigger signals:
  - S1. H8 ≤2 on major surface: no — H8 = 3
  - S2. H4 ≤2 on primary flow: no — H4 = 3
  - S3. Roundtable cross-talk locked on P1+: no — all converged (MOM_design-laws_provider-showcase_2026-09-30.md)
  - S4. 2+ P1 "needs visual exploration": no — F-001/F-002 are copy, F-003 is a menu
- Outcome: Skip

## External Context
_None ingested._

## Discovery Summary

**Confirmed:** 2026-09-30T17:08:04Z (user: "yes")

- **Feature:** Provider Showcase — in-app marketing page per provider: photo (becomes avatar), logo, tagline, About, ≤20 portfolio images, plus a social-network QR.
- **Problem:** Providers cannot show their work or bring their own audience; customers see a name and an alert; there is no provider page, no images, no shareable link.
- **User:** Independent providers who market on Instagram/WhatsApp; customers arriving via scan, code or directory; returning customers re-checking a provider after booking.
- **Success metric:** Per-provider funnel Scans → Opened in app → Booked (self-visits and 24h same-customer repeats excluded); north-star first booking within 7 days of a scan.
- **Technical constraints:** private Azure Blob (Azurite local), Provider-only managed identity, authenticated `/api/v1/media/{hash}`; keys `{providerId}/{sha256}/{full|thumb}`; decode→re-encode every image, JPEG/PNG/WebP ≤5 MB/≤40 MP/≤8000 px; `provider_showcase` collection with targeted writes, DB-enforced cap, permutation-checked reorder; `/api/v1/go/{code}` the only anonymous route (content-free 302, config store URLs, identical on unknown, rate-limited, per-platform counter, no IP/UA); 6-char opaque code, lazy mint, unique; T-004; Gateway rows; unconditional Cloud storage param; erasure + hourly orphan sweep; mobile scanner, QR generation, bearer-token image loader (30-day cache), on-device JPEG conversion, exports on existing graphics stack; EN + es-MX incl. exports; package approvals (Azure.Storage.Blobs, Aspire.Hosting.Azure.Storage, QRCoder, ZXing.Net.Maui, server image library); UGC Report + Hide + Terms clause; UX magazine hero, Dashboard→scanner, one-tap showcase from every provider appearance, relationship-aware showcase; ADRs for storage/serving and sign-in-only + content-free `/go`.
- **Out of scope:** web landing, anonymous API, OG, custom domain, Universal/App Links, deferred deep linking; video, filters, reviews, code re-issue; new-work push; moderation tooling beyond email; RTL layout; mixed-version rollout.
- **Key risks / assumptions:** store listing not live (1hk.14) → config URLs; ZXing.Net.Maui on .NET 10 unverified; image library license open; blob outages / slow networks best-effort; large — 3 waves, possible split at Plan; 30-day client cache after erasure.
