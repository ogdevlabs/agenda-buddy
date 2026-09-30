# PRD: Provider Showcase
<!-- pdlc-template-version: 2.4.0 -->

**Date:** 2026-09-30
**Status:** Approved
**Feature slug:** provider-showcase
**Episode:** <!-- assigned after delivery -->

---

## Overview

Provider Showcase gives every provider an in-app page for marketing their work. It holds a photo that becomes their avatar across the app, an optional logo, a tagline, an About text, and a portfolio of up to 20 images. Each provider also gets a personal QR code and a short code to post on social networks. It serves INTENT's primary persona, the independent service provider, who wins clients on Instagram and WhatsApp today but has nothing in AgendaMe to send them to. It also lifts INTENT's standing exclusion of user-uploaded photos ("until image storage is designed", `agenda-buddy-5qr`) by designing that storage.

---

## Problem Statement

A provider cannot show a customer what their work looks like, and cannot bring their own audience into the app:
- **Customers see almost nothing:** a name and a service count. Tapping a provider in the list opens an alert (`CustomersViewModel.ShowSessionsAsync:286`); there is no provider page at all.
- **No images:** nothing stores them (`INTENT.md:63`), and the only avatar is one of 24 abstract catalogue marks.
- **Nothing to share:** a provider's Instagram bio has no AgendaMe link, code or QR.

The effect is that:
- a tattoo artist, a trainer or a tutor has to be booked on trust, from a text list;
- the provider's biggest marketing channel, their social following, has no route into AgendaMe;
- a customer who booked and then has second thoughts has nowhere to look at the provider's work again.

---

## Target User

- **Primary: the independent service provider** (INTENT persona). Solo professionals, especially visual trades such as tattoo, beauty, fitness and design, who market themselves on social networks and want one place to show their work and send followers to it.
- **Secondary: customers** (INTENT secondary persona):
  - (a) a prospect who scanned a provider's QR, installed the app and registered;
  - (b) a customer browsing the provider directory;
  - (c) an existing, subscribed customer re-checking a provider after booking ("cold feet").

All provider content is visible **only to signed-in users**; there is no anonymous or web audience.

---

## Requirements

**Provider authoring**

1. The system MUST give a provider a **My Showcase** hub, reached from Profile and visible only to providers. It has three cards:
   - **You:** Photo, Logo, Tagline & About.
   - **Your work:** Portfolio.
   - **Share:** Preview as customer, QR & Share.

   The hub MUST show a completeness meter ("N of 5 done").
2. A provider MUST be able to set a **profile photo** from the camera or photo library, cropped to a square. The photo MUST become the provider's avatar wherever an avatar is drawn. The `AvatarCatalog` mark remains the fallback when no photo is set. Replacing the photo MUST detach the previous one.
3. A provider MAY set a **logo**. It appears as a badge on the showcase hero and at the centre of the QR.
4. A provider MUST be able to set a **tagline** (≤80 characters) and an **About** text (≤600 characters). Lengths are counted in grapheme clusters, identically on client and server.
5. A provider MUST be able to keep a **portfolio of 0–20 images**. Each image has an optional caption (≤140 characters) and an optional link to one of the provider's own services. The provider can reorder images, and the first image is the showcase cover.
6. The system MUST refuse a 21st portfolio image atomically, even when two devices add at the same time. The refused upload gets `409` with a human-readable reason.
7. A reorder MUST carry the complete list of image hashes. The server MUST reject (`409`) any list that is not a permutation of the current portfolio.
8. Every showcase write MUST be a targeted update on its own document. None may go through the whole-document provider `PUT` (`agenda-buddy-2wf`, ADR-032).
9. Every showcase write MUST be owner-guarded against the JWT `sub` and the provider role, and MUST audit via `eventStore.SaveAsync` (CONSTITUTION §3).

**Image pipeline**

10. The mobile client MUST convert every selected image to JPEG before upload, including iPhone HEIC. The long edge is downscaled to at most 1600 px.
11. The server MUST accept only JPEG, PNG or WebP, identified by magic bytes, not by extension or `Content-Type`. It MUST reject files over 5 MB, images over 40 megapixels, and images with a side over 8000 px, and it MUST check the dimensions **from the header before decoding the full image**. Each rejection is `400` with a reason.
12. The server MUST decode and **re-encode every image**, keeping only the first frame and normalising to sRGB. Re-encoding strips all EXIF and GPS data. It MUST store two variants, `full` (≤1600 px) and `thumb` (≤480 px). No uploaded byte is ever served as-is.
13. Images MUST be stored in a private blob container under the key `{providerId}/{sha256}/{variant}`, so deduplication happens within one provider only. The Provider service MUST be the only reader and writer, using managed identity in the cloud and Azurite locally.
14. Upload MUST be two steps:
    - (a) `POST` the bytes, which returns `{hash}` and records a `media_ref`;
    - (b) a JSON write attaches the hash to the photo, logo or portfolio.

    Re-uploading identical bytes MUST be idempotent.
15. An in-process hourly sweep MUST delete blobs whose `media_ref` has been unattached for more than 24 hours, and all blobs of an erased provider.

**Serving**

16. Images MUST be served only through the authenticated Gateway route `GET /api/v1/media/{providerRef}/{hash}/{variant}`, with long-lived cache headers. The route MUST require a valid JWT.
17. The mobile client MUST load media through a bearer-token loader with a disk cache keyed by hash. Cached images expire after 30 days, and are evicted when that provider's showcase returns not-found.

**Public code, QR and sharing**

18. Every provider MUST have an opaque **6-character public code**:
    - drawn from a 31-symbol alphabet that excludes 0/O/1/I/L;
    - non-sequential, protected by a unique index, and retried on collision;
    - created on first use, so every existing provider gets one;
    - never derived from, and never revealing, the email address (T-004).
19. `GET /api/v1/go/{code}` MUST be the feature's **only anonymous route**:
    - a `302` to the App Store for iOS browser identifiers and to Google Play otherwise, with the store URLs read from configuration;
    - a static, provider-free "choose your store" page when the platform can't be determined;
    - no provider data in the response, and byte-identical behaviour for known and unknown codes;
    - rate-limited.
20. `/go/{code}` MUST increment an anonymous counter per code and platform (ios / android / other). It MUST NOT store an IP address, a browser identifier or any other identifier.
21. The **QR & Share** screen MUST generate the QR on-device. It encodes the `/go/{code}` URL at high error correction, with the logo at the centre when one is set, and prints the 6-character code beneath. It MUST export three images — Story 1080×1920, Square 1080×1080 and a Printable card — rendered with text localised to the provider's language. It MUST hand them to the system share sheet.

**Customer experience**

22. A signed-in customer MUST be able to open a showcase by:
    - (a) scanning an AgendaMe QR with the in-app scanner;
    - (b) typing a 6-character code;
    - (c) tapping the provider anywhere the provider's name or avatar appears: the providers list, appointment detail, the message-thread header and the booking screen.
23. The **ProviderShowcasePage** MUST show:
    - a cover hero with the avatar and logo badge;
    - the tagline and About;
    - a 3-column portfolio grid of thumbnails that opens a full-screen swipe viewer;
    - the provider's service cards;
    - **Book / Subscribe / Message** pinned outside the scroller.
24. The showcase MUST recognise an existing customer:
    - a "Your next session" banner (Details / Reschedule) when the customer has an upcoming appointment;
    - "Book again";
    - Subscribed ✓;
    - a **NEW** tag on portfolio items added since that customer's last visit.
25. A new customer's Dashboard MUST show a first-run card, "Found a provider? Scan their code", which opens the scanner. After the customer's first subscription or booking, the card MUST collapse into a "Scan a code" row in More.
26. The scanner MUST:
    - offer "Type a code instead";
    - when camera permission is denied, show an explanatory state with **Open Settings** and **Type a code instead**;
    - when the QR isn't an AgendaMe code, answer inline ("That isn't an AgendaMe code") and keep scanning.
27. An unknown, deactivated or erased provider's code MUST resolve to an explicit **not-found** state ("This provider isn't available" + "Scan another code"). That message MUST differ from the network-failure state ("Couldn't load — Try again").
28. No provider content — names, images or text — MUST be reachable by an unauthenticated caller, apart from the content-free `/go` redirect.

**Attribution**

29. Opening a showcase MUST record a visit `{provider, customer, source ∈ scan|code|directory|appointment|message|booking, at}`. The provider's own visits MUST NOT be counted, and repeat visits by the same customer within 24 hours are counted once.
30. The My Showcase hub SHOULD show the funnel **Scans → Opened in app → Booked**, where a booking counts when it is made within 7 days of a scan or code open.

**User-generated-content safety (App Review 1.2)**

31. Every showcase MUST offer **Report**, with the reasons inappropriate / not their work / spam / other. It stores a `showcase_report` and emails the operator address from configuration.
32. A customer MUST be able to **Hide this provider**. A hidden provider no longer appears in that customer's directory, and resolves to not-found for that customer through scan and code.
33. The Terms MUST gain a provider-content clause and a contact address. The Privacy Policy MUST name profile photos, logos, portfolio images and visit records as collected data (`LegalDocumentsTest`).

**Erasure**

34. Account erasure (`AccountErasureService`) MUST delete the provider's showcase document, every blob under their prefix, their `media_ref`s, visit records and reports. It deletes the document first, then the blobs; if a blob deletion fails, the sweep finishes it, and the route still answers `204`. Erasing a customer MUST delete that customer's visits, reports and hides.

**Localisation and documentation**

35. Every new user-facing string MUST exist in `AppResources.resx` and `AppResources.es-MX.resx` (`LocalizationResourceTest`), including the text rendered into exported images.
36. Every new route MUST be added to the Gateway allowlist, the Bruno collection (`9-Showcase`) and the regenerated OpenAPI baselines.

---

## Assumptions

- The App Store and Play listings will exist before providers print QR codes in earnest (`agenda-buddy-1hk.14`). Until then the configured store URLs can point at TestFlight or internal testing, and `/go`'s indirection means printed QR codes never need reprinting.
- A 6-character code over 31 symbols (about 887 million values) with rate limiting is enough to make enumeration impractical for a product of this size.
- Customer devices can cache up to 20 thumbnails per showcase without memory pressure at 480 px.
- SkiaSharp (or `Microsoft.Maui.Graphics`) is already reachable in the mobile dependency graph, so share images need no new top-level package. Design verifies this.
- ZXing.Net.Maui (or an equivalent) supports .NET 10 MAUI on iOS and Android. Design verifies this; if it doesn't, platform-native scanners (VisionKit / ML Kit) behind an interface are the fallback.
- Operators can handle report emails by hand at current volumes; no moderation console is needed yet.
- Azure Blob Storage with managed identity is acceptable cost-wise for dev, and one `provision: true` deploy is acceptable.

---

## Acceptance Criteria

**Authoring and persistence (R1–R9)**
1. A provider reaches My Showcase from Profile. A customer account has no such row, and gets `403` from every showcase write route. 🧪 test-first
2. After a provider sets a photo, the avatar in BrandHeader, the providers list, the message thread and the showcase shows that photo. With no photo, it shows the `AvatarCatalog` mark. 🧪 test-first
3. A tagline of 81 grapheme clusters or an About of 601 is rejected with `400` naming the field. Exactly 80 / 600, including multi-codepoint emoji, is accepted. 🧪 test-first
4. With 20 portfolio images, a further add returns `409` "Your portfolio is full (20 of 20)". Two concurrent adds at 19 images leave exactly 20 stored (integration, real MongoDB). 🧪 test-first
5. A reorder whose hash list is not a permutation of the stored portfolio returns `409` and changes nothing. A reorder identical to the stored order returns `200` (the `MatchedCount` lesson). 🧪 test-first
6. Changing a caption or reordering leaves `ProviderEntity` untouched: every nested service id is unchanged afterwards. 🧪 test-first
7. Every showcase command handler writes an audit event (`EventStoreWriteGuardTest` covers the new handlers). 🧪 test-first

**Image pipeline (R10–R15)**
8. An upload whose bytes are a renamed executable, a polyglot, or a JPEG/PNG/WebP whose magic bytes don't match the declared type is rejected with `400`. 🧪 test-first
9. A 5 MB + 1 byte file, a 40 MP + 1 image, or an 8001 px side is rejected with `400`. The dimension checks are made before the full decode, which a test asserts by showing a decompression-bomb fixture never allocates its full pixel buffer. 🧪 test-first
10. A JPEG carrying GPS EXIF comes back from `/media` with no EXIF segment at all. An animated WebP or GIF-in-WebP comes back as a single frame. 🧪 test-first
11. Each accepted upload produces exactly two blobs, `{providerId}/{sha256}/full` and `.../thumb`, with the long edges ≤1600 and ≤480 px. 🧪 test-first
12. Uploading the same bytes twice returns the same hash and creates no extra blob. The same bytes uploaded by two providers produce two separate blobs. 🧪 test-first
13. A blob whose `media_ref` has been unattached for 24 hours + 1 minute is deleted by the sweep; one at 23 hours is not. 🧪 test-first
14. On the mobile client, a HEIC image selected from the library is uploaded as a JPEG with a long edge ≤1600 px. 🧪 test-first

**Serving (R16–R17)**
15. `GET /api/v1/media/...` without a JWT returns `401` through the Gateway. With any valid JWT it returns the image with an immutable `Cache-Control`. 🧪 test-first
16. The mobile media loader sends the bearer token, serves a second request for the same hash from the disk cache, and re-fetches after 30 days. 🧪 test-first

**Code, `/go`, QR (R18–R21)**
17. Every provider, including one created before this feature, gets a 6-character code from the 31-symbol alphabet on first request. Two providers never share a code (unique index, collision retry tested with a forced collision). 🧪 test-first
18. `GET /api/v1/go/{code}` without a JWT returns `302`: to the configured App Store URL for an iPhone browser identifier, and to the configured Play URL for an Android one. An unknown or undeterminable platform gets the static chooser page. 🧪 test-first
19. `/go` returns byte-identical responses (status, headers other than `Date`, body) for a known and an unknown code. The response contains no provider name, email, image or id. 🧪 test-first
20. Each `/go` hit increments `{code, platform}` by one. No stored document contains an IP address or browser identifier. `/go` is rate-limited when `Security:RateLimiting:Enabled` is true. 🧪 test-first
21. The generated QR decodes to exactly the provider's `/go/{code}` URL, including with the logo overlaid. The Story, Square and Printable exports are 1080×1920, 1080×1080 and the card size, carry the printed code, and show es-MX text for an es-MX user. 🧪 test-first

**Customer experience (R22–R28)**
22. From the Dashboard, a new customer reaches a showcase in two taps (the card, then scanning or typing a valid code). The card collapses to a More row after their first subscription or booking. 🧪 test-first
23. Tapping a provider in the providers list, on appointment detail, in the message-thread header, or on the booking screen opens that provider's showcase; the providers-list alert is gone. 🧪 test-first
24. Book / Subscribe / Message are outside every scroller on ProviderShowcasePage (`PinnedActionTest`). The portfolio grid is not a `CollectionView` nested in a `ScrollView` (`ScrollableListNestingTest`). The page carries `BrandHeader` (`BrandHeaderPresenceTest`), and its route is registered (`ShellRouteRegistrationTest`). 🧪 test-first
25. For a customer with an upcoming appointment, the showcase shows "Your next session" with that appointment's time and "Book again". Items added after the customer's previous visit carry NEW. 🧪 test-first
26. With camera permission denied, the scanner shows Open Settings and Type a code instead. A non-AgendaMe QR shows "That isn't an AgendaMe code" and the scanner keeps running. 🧪 test-first
27. An unknown, deactivated, erased or hidden-by-this-customer provider's code shows "This provider isn't available". A request that failed on the network shows "Couldn't load — Try again". 🧪 test-first
28. No route other than `/go` returns any provider showcase data without a JWT (integration sweep over every new route). 🧪 test-first

**Attribution (R29–R30)**
29. A provider opening their own showcase records no visit. The same customer opening it twice within 24 hours records one visit; opening it after 24 hours records two. 🧪 test-first
30. The hub's funnel counts a booking made 6 days after a scan and ignores one made at 8 days. 🧪 test-first

**UGC safety (R31–R33)**
31. Report stores a `showcase_report` with the reason and emails the configured operator address. The email failing does not fail the report. 🧪 test-first
32. After "Hide this provider", that provider is missing from the customer's directory, and scanning their code shows not-found for that customer only. 🧪 test-first
33. `LegalDocumentsTest` passes with the Privacy Policy naming photos, logos, portfolio images and visit records, and the Terms containing the provider-content clause and a contact address. 🧪 test-first

**Erasure (R34)**
34. After provider erasure, the showcase document, every blob under the prefix, all `media_ref`s, visits and reports are gone, and the route returned `204`. With blob deletion forced to fail, the route still returns `204` and the next sweep leaves no blobs (integration, Azurite). 🧪 test-first
35. After customer erasure, that customer's visits, reports and hides are gone. 🧪 test-first

**Localisation and docs (R35–R36)**
36. `LocalizationResourceTest` passes with every new key present in both resource files. 🧪 test-first
37. The Gateway forwards `/api/v1/media/**` and `/api/v1/go/**` (`AspireServiceDiscoveryProxyConfigProvider` route test). The OpenAPI baselines are regenerated with the baseline writer, and `OpenApiSpecDriftTest` passes. Bruno has a `9-Showcase` folder. 🧪 test-first

---

## User Stories

**F-037-US-01: Provider builds a showcase**
*Acceptance criteria: 1, 2, 3, 6, 7*
Given a signed-in provider on Profile
When they open My Showcase, set a photo, a logo, a tagline and an About, and save each
Then each is stored by its own targeted write
And the photo replaces their avatar everywhere
And the completeness meter reads "4 of 5 done"

**F-037-US-02: Provider curates a portfolio**
*Acceptance criteria: 4, 5, 11, 12, 14*
Given a provider with 19 portfolio images
When they add one iPhone photo and drag it to first place
Then it is uploaded as a JPEG, stored as full and thumb, and becomes the cover
And a further add is refused with "Your portfolio is full (20 of 20)"

**F-037-US-03: Hostile or careless upload is neutralised**
*Acceptance criteria: 8, 9, 10, 13*
Given a provider uploading a file
When it is a renamed executable, a decompression bomb, an oversize image, or a JPEG with GPS EXIF
Then the first three are rejected with 400 and a reason
And the JPEG is stored re-encoded with no EXIF
And an abandoned upload is swept after 24 hours

**F-037-US-04: Provider shares a QR on Instagram**
*Acceptance criteria: 17, 21*
Given a provider on QR & Share
When they choose "Story" and share it
Then the share sheet receives a 1080×1920 image carrying the QR, their logo, their 6-character code and localised text
And the QR decodes to their /go/{code} URL

**F-037-US-05: Prospect scans a QR without the app**
*Acceptance criteria: 18, 19, 20*
Given someone with no AgendaMe app scans a provider's QR with their iPhone camera
When the browser opens /go/{code}
Then they are redirected to the App Store
And the scan is counted for that code and "ios", without storing anything about the person
And an unknown code behaves identically

**F-037-US-06: New customer finds the provider after registering**
*Acceptance criteria: 22, 26, 27*
Given a newly registered customer on the Dashboard
When they tap "Found a provider? Scan their code" and scan the same QR (or type the printed code)
Then the provider's showcase opens
And a denied camera, a foreign QR or an unknown code each leads to a clear state with a way forward

**F-037-US-07: Customer is persuaded by the showcase**
*Acceptance criteria: 23, 24, 15, 16*
Given a signed-in customer on a provider's showcase
When they scroll the portfolio and open an image full-screen
Then thumbnails load from an authenticated, cached media route
And Book, Subscribe and Message stay pinned on screen

**F-037-US-08: Customer with cold feet revisits the work**
*Acceptance criteria: 23, 25*
Given a customer with an upcoming appointment with a provider
When they tap the provider chip on appointment detail
Then the showcase opens with "Your next session" and "Book again"
And the pieces added since their last visit are marked NEW

**F-037-US-09: Provider sees their QR working**
*Acceptance criteria: 29, 30*
Given a provider whose QR was scanned 40 times, with 12 in-app opens and 3 bookings within 7 days
When they open My Showcase
Then the funnel reads Scans 40 → Opened 12 → Booked 3
And their own visits are not counted

**F-037-US-10: Customer reports or hides a provider**
*Acceptance criteria: 31, 32, 33*
Given a customer on a showcase they find inappropriate
When they choose Report → "not their work", and then Hide this provider
Then a report is stored and emailed to the operator
And that provider disappears from the customer's directory and scan results

**F-037-US-11: Nothing leaks to the signed-out world**
*Acceptance criteria: 19, 28*
Given an unauthenticated caller
When they call any showcase or media route
Then they receive 401, apart from /go, which returns only a store redirect

**F-037-US-12: Erased provider leaves nothing behind**
*Acceptance criteria: 34, 35*
Given a provider with a full showcase
When they delete their account
Then the showcase, images, codes' visits and reports are removed and the route answers 204
And a blob-store failure during deletion is finished by the next sweep

---

## Testing Approach: Test-Driven Development (TDD)

**Tests are written first.** During Construction (`/build`), for **every acceptance criterion above**, a **failing test is written and run before any implementation code** — the Red → Green → Refactor cycle:

1. **Red** — write the smallest failing test that pins the acceptance criterion, named with the Given/When/Then language from the matching user story. Run it; confirm it fails for the right reason (logic not implemented — not a syntax/import error).
2. **Green** — write the minimum implementation that makes the test pass. Run the test and the full suite; no regressions.
3. **Refactor** — clean up without changing behavior; suite stays green.

The build loop enforces this at a mandatory **TDD gate** (build Step 9a-bis): implementation code for a criterion may not be written until a failing test for it exists. The only exceptions are pure scaffolding, config-only, and infrastructure-only work — and even those require an **explicit human TDD override**. There is no silent skip. (TDD can be disabled only by editing `CONSTITUTION.md` § Test Gates — the Constitution always wins.)

**Security acceptance criteria are enforced mechanically (issue #55).** Any `[security]`-tagged criterion above (threat-derived, materialized on its task via `tasks.cjs ac add`) is not just governed by the prose gate: `node scripts/tasks.cjs done` **structurally refuses** to close a task whose `[security]` AC has no linked test. Name each security test after its threat id (`test_TNNN_…`) and link it with `tasks.cjs ac link-test`. This makes it impossible to close a threat mitigation on a citation alone — the failure mode where a mitigation lived as a task-body reference while its acceptance criterion was never written, and TDD had nothing to bite on.

**Test layers** for this feature:
- **Unit:** backend `agenda-buddy-backend.slnf` and `AgendaBuddy.MobileApp.Tests`.
- **Integration:** `AgendaBuddy.IntegrationTests` against MongoDB **and Azurite** Testcontainers.
- **Security scan:** dependency audit + gitleaks, always on; the new packages must pass the audit.

E2E, Performance, Accessibility and Visual Regression are unchecked in CONSTITUTION §7. Accessibility is still asserted structurally (caption-as-description, 44pt targets) in mobile unit tests, and the Android TFM build is run locally for any `#if MOBILE` / `#if ANDROID` code.

---

## User Experience

### Layout

**Selected: Option A, magazine hero plus grid.**
- The cover image sits in ProfilePage's `BrandGradient` hero slot, with the avatar overlapping bottom-left and the logo badge bottom-right.
- Below it: name, tagline, a 3-column portfolio grid (thumbnails; a tap opens a full-screen swipe viewer), then service cards.
- Book / Subscribe / Message are pinned outside the scroller (the `PinnedActionTest` pattern). `BrandHeader` is the first element.
- Visual companion fragment: `.pdlc/brainstorm/provider-showcase-44879-1790785256/content/ux-discovery-q1-q3.html`
- Accessibility:
  - 44pt minimum targets;
  - pinned actions announce "Book with {name}";
  - each portfolio image's caption is its accessible description, with a generic label when there's no caption;
  - text over the hero sits on a ≥4.5:1 scrim;
  - focus order is back → name → portfolio → services → actions.

**My Showcase hub:** three cards.
- *You:* Photo, Logo, Tagline & About.
- *Your work:* Portfolio.
- *Share:* Preview as customer, QR & Share.

A completeness meter ("N of 5 done") sits above them; the grouping is the cognitive-load fix from the UX quality pass.

### User Flow

**Selected: Option A, Dashboard card plus a dedicated scanner.**
- **New customer:** Dashboard first-run card "Found a provider? Scan their code" → `ScanProviderPage` (camera + "Type a code instead") → `ProviderShowcasePage` → the existing `BookAppointmentPage`. That's two taps. After the first subscription or booking, the card collapses to a "Scan a code" row in More.
- **Provider:** Profile → My Showcase hub → Photo · Logo · Tagline & About · Portfolio · Preview as customer · QR & Share (Story / Square / Printable → share sheet).
- **Returning, cold-feet customer (user-raised Q2b):** the rule is that wherever the provider's name or avatar appears, one tap opens their work.
  1. AppointmentDetailPage: the provider becomes a "See {FirstName}'s work" chip with a 4-image strip, placed above Reschedule/Cancel.
  2. The showcase recognises the customer: a "Your next session" banner (Details / Reschedule) replaces the Book hero; "Book again"; Subscribed ✓; NEW tags on items added since the customer's last visit.
  3. A providers-list row tap opens the showcase (replacing the `ShowSessionsAsync` alert), with an "N NEW" chip.
  4. MessageThreadPage: the header avatar and name open the showcase.
  5. BookAppointmentPage: a 4-image strip at the top, to reassure before confirming.

Fragments: `.pdlc/brainstorm/provider-showcase-44879-1790785256/content/ux-discovery-q1-q3.html`, `.pdlc/brainstorm/provider-showcase-44879-1790785256/content/returning-customer-v1.html`, `.pdlc/brainstorm/provider-showcase-44879-1790785256/content/journey-map-v2.html`

### State Coverage

- **Empty:**
  - No portfolio: the cover falls back to the provider's photo, then to the brand gradient with the avatar mark. Services still show.
  - Provider hub: an "Add your first work" call to action.
- **Loading:** skeleton hero and tiles. The cover thumbnail loads first; tiles fade in progressively.
- **Error:**
  - Not found: "This provider isn't available" + "Scan another code".
  - Network failure: "Couldn't load — Try again".
  - The two messages differ, following AppointmentDetailPage.
- **Success:** the full showcase. After a scan, a one-time snackbar reads "You found {name} through their code" (via `IInAppAlertService`).
- **Scanner:**
  - Camera denied: Open Settings / Type a code instead.
  - Foreign QR: inline "That isn't an AgendaMe code", with the scanner still open.
- **Uploads:** progress and retry per item. Leaving the editor mid-upload asks "N uploads still in progress — leave anyway?".

### Design Deviations

No deviations: every selection composes existing components and patterns. The story-style viewer (Option B) was the only new pattern offered, and it wasn't selected; the full-screen viewer reached from a grid tile is a standard swipe pager.

---

## Non-Functional Requirements

- **Performance:**
  - Showcase first contentful render (hero + first grid row, from thumbnails) ≤1.5 s on a warm cache over 4G.
  - `/go` answers in ≤100 ms at the server.
  - Server image processing ≤2 s per image at the 40 MP cap.
- **Memory:** the grid decodes thumbnails only. The full-size variant is decoded only in the full-screen viewer, one image either side of the current one.
- **Security:**
  - Only `/go` is anonymous, and it is content-free.
  - Every uploaded image is re-encoded.
  - Blob container private; access through managed identity only, with no account keys in configuration.
  - No email in any URL (T-004).
  - `PiiRedactingProcessor` must still cover the new routes.
  - `/go` is rate-limited per IP when rate limiting is on.
- **Privacy:**
  - The `/go` counter holds no identifiers.
  - Visits are stored against account ids and erased with the account.
  - The Privacy Policy is updated.
- **Accessibility:**
  - 44pt targets; captions as descriptions.
  - A code-entry alternative to the camera.
  - ≥4.5:1 contrast for text over images.
- **Reliability:** image channels are best-effort. A blob outage shows placeholder tiles, never a crash or a blank page.
- **Operability:**
  - The storage connection is an unconditional Cloud-shape parameter, and the local Azurite runs through Aspire.
  - Store URLs and the operator email are configuration.
  - Each service warns at startup when a key is missing.
- **Dependencies:** each new package needs explicit approval (CONSTITUTION §9) and must pass `dotnet list package --vulnerable`.

---

## Out of Scope

- **Web landing page, anonymous public API, Open Graph previews, custom domain, Universal/App Links:** a user decision (Discover Round 2 Q3). All provider content sits behind app sign-in.
- **Deferred deep linking (passing the code across install):** it needs device fingerprinting or clipboard writes, which is privacy-hostile. The printed code and the Dashboard card cover the gap.
- **Video, filters, and editing beyond a square crop:** not needed to prove the showcase's value.
- **Reviews and ratings:** a separate trust feature, with its own moderation needs.
- **Re-issuing or customising a code:** printed codes are permanent, and re-issue would orphan printed material.
- **A "new work added" push notification:** a marketing message, which needs an opt-in and a new `NotificationType`.
- **An operator moderation console:** reports are emailed and handled by hand at current volume.
- **RTL-specific layout:** any script is accepted; layout isn't mirrored.
- **Mixed old/new app versions during rollout:** client release is gated by the store, and the backend is additive and nullable.
- **Customer profile photos:** the photo-as-avatar work is provider-only here. Customers keep catalogue avatars.

---

## Known Risks

- **Blob store outage (edge case 10):** uploads fail and tiles show placeholders. Deferred because best-effort with per-item retry is proportionate; there is no offline mode.
- **Slow networks (edge case 12):** mitigated by thumbnails and progressive load. Deferred: no offline mode.
- **App Store / Play listings not live (`agenda-buddy-1hk.14`):** `/go` reads its targets from configuration and can point at TestFlight or internal testing meanwhile. Printed QR codes survive the switch.
- **Scanner package on .NET 10 MAUI is unverified:** Design spikes ZXing.Net.Maui. The fallback is VisionKit / ML Kit behind an `IQrScanner` interface.
- **Server image library license:** the ImageSharp split license vs SkiaSharp vs Magick.NET. It's decided at package approval in Design.
- **Cached images after erasure:** customer devices may keep a thumbnail for up to 30 days, or until the showcase is next opened and returns not-found. Accepted as proportionate.
- **Any signed-in user can fetch any media hash:** hashes are unguessable and the content is meant for showing, so this is accepted and recorded in the threat model.
- **Size:** this is the largest feature since F-035. It's planned in three waves (W1 storage + authoring + showcase; W2 code + `/go` + scanner + QR/share; W3 attribution, returning-customer polish and UGC safety), and the Plan step may split it.

---

## Standards Alignment

_Not applicable. The Nordstrom Standards Readiness gate does not apply to this project (ADR-042, CONSTITUTION §9)._

---

## Design Docs

- Architecture: [ARCHITECTURE.md](../design/provider-showcase/ARCHITECTURE.md)
- Data model: [data-model.md](../design/provider-showcase/data-model.md)
- API contracts: [api-contracts.md](../design/provider-showcase/api-contracts.md)
- Threat model: [threat-model.md](../design/provider-showcase/threat-model.md)
- UX review: [ux-review.md](../design/provider-showcase/ux-review.md)
- Additional: brainstorm log `docs/pdlc/brainstorm/brainstorm_provider-showcase_2026-09-30.md`; progressive-thinking MOM `docs/pdlc/mom/provider-showcase_progressive-thinking_mom_2026_09_30.md`; threat-model MOM `docs/pdlc/mom/MOM_threat-model_provider-showcase_2026-09-30.md`; design-laws MOM `docs/pdlc/mom/MOM_design-laws_provider-showcase_2026-09-30.md`

---

## Related Episodes

- The account-erasure episode (`AccountErasureService` / `AccountErasure`): the showcase and blobs join the erasure set.
- The avatar catalogue (ADR-064): the photo becomes the first-choice avatar, and the catalogue stays as the fallback.
- F-015 API Gateway: the new `/media` and `/go` allowlist rows.
- F-035 marketplace payments: the precedent for a content-free public HTTPS redirect (`PaymentWebhookModule.cs:16-26`).

---

## Approval

**Approved by:** ogdevlabs
**Date approved:** 2026-09-30
**Notes:**
