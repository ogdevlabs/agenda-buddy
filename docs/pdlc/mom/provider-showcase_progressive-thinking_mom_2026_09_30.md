---
feature: provider-showcase
topic: progressive-thinking
date: 2026-09-30
mode: solo
participants: Atlas, Neo, Echo, Phantom, Bolt, Friday, Muse, Pulse, Jarvis
---

# Meeting Minutes: Progressive Thinking
## Feature: provider-showcase (F-037) | 2026-09-30

**Mode:** Solo
**Participants:** Atlas (Product Manager, facilitator), Neo (Architect), Echo (QA), Phantom (Security), Bolt (Backend), Friday (Frontend), Muse (UX, weighted voice at the user's request), Pulse (Deployment), Jarvis (Tech Writer)

---

## Context

F-037 gives a provider a marketing showcase: a photo that becomes their avatar, an optional logo, a tagline, an About text, and up to 20 portfolio images. It also gives them a QR code to post on social networks. Three Socratic rounds (Sketch mode, all accepted) set the shape:
- **Storage:** private Azure Blob, reached only by the Provider service, served through an authenticated `/api/v1/media/{hash}`.
- **Sign-in:** all provider content sits behind sign-in, with no web page, no public API and no custom domain (user override).
- **QR:** points at one anonymous, content-free 302 `/api/v1/go/{code}` that sends the visitor to the right store. The same QR is scanned in-app after registration to open the showcase.
- **Customer screen:** a new `ProviderShowcasePage`.
- **Attribution:** in-app, by source (`scan`, `code` or `directory`).

---

## Discussion

### Round 1 — Concrete (what do we know for certain?)

**Bolt:**
- `ProviderEntity` has no bio, image, logo or public-code fields; `avatar_id` is at `ProviderEntity.cs:80-81`.
- The profile `PUT` replaces the whole document and resets nested service ids (`agenda-buddy-2wf`). Every write in this feature has to be a targeted `$set`, or live in its own collection.
- `FindOneAndUpdateAsync` is the only partial-update primitive (ADR-032).
- There is no multipart/`IFormFile` handling anywhere in the backend today.

**Neo:**
- There is no blob storage anywhere; the only Azure Storage account holds tfstate.
- The Gateway only forwards what `_routeSpecs` lists (`AspireServiceDiscoveryProxyConfigProvider.cs:67-84`). Both `/media` and `/go` need rows there or they are silently unreachable.
- MongoDB.Driver stays pinned at 2.25 and nothing here needs it to move.
- CONSTITUTION §9 requires approval for every new package.

**Phantom:**
- Every provider route is authenticated today; no provider route allows anonymous access.
- An email must never appear in a URL (threat T-004); `PiiRedactingProcessor` exists because `url.path` leaked addresses.
- Account erasure must remove everything an account owns (`AccountErasure.cs`, App Review 5.1.1(v)).

**Friday:**
- There is no provider detail page: `CustomersViewModel.ShowSessionsAsync:286` shows an alert.
- There is no MediaPicker, no camera or photo-library permission, and no QR or barcode package in `AgendaBuddy.MobileApp.csproj` (only CommunityToolkit.Maui 9.1.1, Mvvm, Plugin.Firebase).
- `Share.RequestAsync` is unused.
- Avatars resolve through `AvatarCatalog` (24 ids).

**Muse:**
- The user's words were "UX marketing immersive offering".
- Signed-out visitors must install and register first, and nothing works outside the app.
- Three accepted acceptance criteria are Muse's responsibility: ≤2 taps from Home to a showcase, the three actions pinned, and a clear not-found state.

**Echo:**
- `LocalizationResourceTest`, `BrandHeaderPresenceTest`, `ShellRouteRegistrationTest`, `PinnedActionTest`, `ScrollableListNestingTest` and `TwoWayBindingTargetTest` will all see the new views and must stay green.
- Only the integration suite can prove persistence behaviour (see the `MatchedCount` lesson).

**Pulse:**
- Dev deploys via azd plus Terraform.
- A parameter the Cloud shape declares conditionally is never declared at all. Any new storage parameter must be unconditional in the Cloud shape.
- A new Azure resource needs `provision: true` once.

**Jarvis:**
- A route change means Bruno and OpenAPI baselines must change too (feedback memory).
- INTENT.md:63 lists user-uploaded photos as out of scope "until image storage is designed". This feature *is* that design, so it needs an ADR plus an INTENT update.

**Atlas synthesis:**
- The feature starts from zero on storage, uploads, the public code, the scanner and the showcase page.
- The constraints that bind are targeted writes, the Gateway allowlist, T-004, erasure, package approval and the unconditional Cloud parameter.

### Round 2 — Inferential (what can we reasonably infer?)

**Bolt:**
- The portfolio should be an **embedded array on a separate `provider_showcase` document keyed by provider**, not fields on `ProviderEntity`. That keeps the showcase out of the whole-document provider `PUT` path entirely.
- Enforce the 20 cap atomically by filtering on `$expr: {$lt: [{$size: "$portfolio"}, 20]}` inside the `$push`.

**Neo:**
- Uploads should be **two steps: bytes first, then metadata.** `POST /media` takes the image, validates it, strips EXIF and returns `{hash}`. A separate JSON `PUT` then references hashes.
- Keeping bytes out of the showcase document keeps reorder and caption edits cheap, and makes content-addressing deduplicate naturally.

**Phantom:**
- `/go/{code}` must be **genuinely content-free**: same response shape for a known and an unknown code, or it becomes a code-enumeration oracle.
- Codes must not be sequential: 6 chars from a 31-symbol alphabet with no 0/O/1/I/L gives about 887M combinations.

**Friday:**
- Scanning is likeliest done with the ZXing.Net.Maui camera view, which needs package approval, camera permission and `NSCameraUsageDescription`.
- The QR can be *generated* with pure-C# QRCoder into a PNG with no platform code.
- Exports compose on SkiaSharp, which MAUI already carries transitively. Verify before relying on it.

**Muse:**
- **The scan only works for a customer if it can survive the install-and-register gap.** Asking them to scan the same poster twice feels broken, and on a phone they may no longer have the poster (it was an Instagram story).
- Infer a **deferred code**: after registration, a new customer's Home shows a first-run "Found a provider? Scan or type their code" card. The printed code under the QR is the insurance.

**Pulse:**
- Azurite locally via `Aspire.Hosting.Azure.Storage`'s emulator, and a Storage account plus private container in the Terraform/Bicep graph for dev.
- Managed identity with the `Storage Blob Data Contributor` role on the Provider app only.

**Echo:**
- Magic-byte and EXIF-strip checks need real fixture images committed to the test project: a JPEG with GPS EXIF, a PNG, a WebP, a polyglot and a renamed `.exe`.

**Disagreements flagged for Round 5:**
- (a) Muse's deferred code vs Phantom's content-free `/go`.
- (b) Friday's SkiaSharp export vs Neo's "no new rendering stack".

### Round 3 — Consequential (what follows?)

**Bolt:**
- New: the `provider_showcase` collection and repository.
- New: `ShowcaseService`, plus a `MediaService` over `IBlobStore` (interface in Library, Azure implementation behind it, and an in-memory implementation for unit tests).
- Handlers in `AgendaBuddy.Provider.Core` for the showcase commands, each auditing via `eventStore.SaveAsync`.
- A public-code generator with a unique index on `public_code`.
- The `/go` 302 module and a `showcase_visits` collection for attribution.

**Friday:**
- Seven new views: `MyShowcasePage`, `PortfolioEditorPage`, `ProviderShowcasePage`, `PortfolioViewerPage`, `QrSharePage`, `ScanProviderPage` and `EnterProviderCodePage`.
- A directory-row tap is rewired to open the showcase instead of the alert.
- A Home first-run card.
- `AvatarSource` resolves a photo hash first and falls back to the catalogue.
- An authenticated image loader: MAUI's `UriImageSource` cannot send a bearer token, so images need a stream-based source with the JWT and a disk cache keyed by hash.

**Phantom:** the new attack surface is:
- Upload parsing: decompression bombs, polyglots, EXIF location leaks.
- An anonymous `/go`: enumeration, an open redirect if the target is ever caller-supplied, rate limiting.
- Media fetch: any signed-in user can fetch any hash. That is acceptable because hashes are unguessable and the content is intended for showing, but it needs recording as an accepted risk.

**Pulse:**
- New Storage account, container, role assignment and a `storage` connection parameter.
- `deployable` filter entries if any new project appears.
- An `ImageSharp` license check, if it is chosen for EXIF strip and re-encode.

**Echo:**
- Integration tests: cap-at-20 under concurrent pushes, erasure removes blobs, a no-op reorder returns 200 (the `MatchedCount` lesson).
- Unit tests: code alphabet and uniqueness retry, `/go` identical response for unknown codes, UA routing.
- Structural tests: every new view passes the suites above.

**Muse:**
- Every screen needs empty, loading, error and not-found states designed, not left as leftovers.
- The provider hub needs a completeness meter ("3 of 5 done"), so that "immersive" does not ship as an empty grey page for the 90% of providers who never upload.

**Jarvis:**
- ADR for image storage and serving.
- ADR for sign-in-only showcase plus the content-free `/go`.
- INTENT update.
- Bruno: new `9-Showcase` folder.
- OpenAPI baselines regenerated with the test-writer, never with the script.
- Privacy policy (`LegalDocuments`) must now name photos and portfolio images. `LegalDocumentsTest` enforces naming.

### Round 4 — Speculative (what might we be missing?)

**Phantom:**
- What if a provider uploads someone else's work, or explicit content? There is no moderation.
- At minimum: a "Report this showcase" action for customers, and Terms language on provider-owned content. App Review Guideline 1.2 expects user-generated-content apps to offer reporting and blocking.

**Muse:**
- What if the provider has no professional photos? The cover falls back to their photo, then to a brand gradient with the avatar mark. The showcase must still look intentional with zero images.
- What if a customer opens a showcase for a provider in another city? Nothing to do now; note it.

**Pulse:**
- What if the App Store listing is not live yet (`agenda-buddy-1hk.14`)? `/go` needs its store URLs from **configuration**, with a sensible fallback.
- There are no store URLs anywhere in the repo today.

**Echo:**
- What if the phone is an iPad, or a desktop scanning from a laptop screen? The UA routing needs a defined default.
- Proposed default: a tiny static "Get AgendaMe on iPhone / Android" chooser *with no provider data*. That would be the only HTML anywhere, and it stays content-free.

**Neo:**
- What about a later feature that wants public web profiles? The public code and the content-addressed media are exactly what it would reuse, so today's design does not block that door.

**Bolt:**
- What about orphaned blobs, uploaded but never attached, or detached by a reorder or delete? Needs a reference check on delete, or a periodic sweep.

**Friday:**
- Upload over a poor mobile connection: 20 × 400 KB is fine, but each upload needs its own progress and retry, not all-or-nothing.

### Round 5 — Conflicting (where do we disagree?)

**Conflict 1: deferred code vs content-free `/go`**
- Muse wants `/go` to hand the code to the app across the install gap.
- Phantom notes that true deferred deep linking needs fingerprinting or a clipboard write, both privacy-hostile, and any stored hand-off state stops `/go` being content-free.
- **Resolved:** no deferred deep linking. The QR payload *is* the `/go` URL, so a customer who installs and returns to the same QR scans it in-app. The printed 6-char code and the Home first-run card cover the lost-poster case. Muse accepted: the code under the QR makes this recoverable without fingerprinting. Phantom accepted the Home card, which holds no state.

**Conflict 2: export rendering stack**
- Friday wants SkiaSharp for Story, Square and Printable composites; Neo does not want a new rendering dependency.
- **Resolved:** confirm in Design whether SkiaSharp is already transitive via CommunityToolkit.Maui/MAUI graphics. If it is, use it. If not, fall back to `Microsoft.Maui.Graphics` `PlatformImage`/`ICanvas` drawing, which ships with MAUI. Either way, no new top-level package just for export.

**Conflict 3: EXIF strip library**
- Bolt proposes SixLabors.ImageSharp; Pulse flags its Six Labors Split License (commercial terms above a revenue threshold).
- **Not resolvable internally** — it is a licensing and business call. Escalation is deferred to the Define/Design package-approval step, where all new packages are presented together:
  - ImageSharp (license)
  - SkiaSharp server-side re-encode (MIT, a heavier native dependency)
  - Magick.NET (Apache-2)
  - Server-side the preferred order is SkiaSharp then Magick.NET, because both re-encode, which strips all metadata by construction.

### Round 6 — Strategic (what must we get right?)

Ranked design priorities:
1. **Upload safety pipeline** (Phantom, Bolt): magic bytes → decode → re-encode (metadata stripped by construction) → size and pixel caps → content hash → blob. Every image is re-encoded; no uploaded byte is ever served as-is.
2. **Content-free anonymous surface** (Phantom): `/go/{code}` is the only anonymous route, returns a 302 to configured store URLs with identical behaviour for an unknown code, and is rate-limited. Everything else requires a JWT.
3. **The customer's first 10 seconds** (Muse): Home → scan/code → showcase in ≤2 taps; the hero loads the cover first and progressively; every state is designed; Book/Subscribe/Message pinned. This is where "immersive" is won or lost.
4. **Targeted, atomic writes plus complete erasure** (Bolt, Echo): its own collection, a `$push` guarded by `$size`, reorder by a whole-array `$set` of hashes validated against the current set, erasure deleting the document, the blobs and the visits.
5. **Authenticated image loading on the client** (Friday): a bearer-token stream loader with a hash-keyed disk cache. Without it every image fails, and nothing flags it at build time.

**Safely deferred:**
- Moderation beyond a report action.
- Analytics beyond visit counts.
- Orphan sweep: reference-check on delete now, sweep later.
- iPad-specific layout.

---

## Conclusion

1. **Confirmed facts:** no storage, no multipart, no public routes, no detail page, no media or QR packages; targeted writes are mandatory (`2wf`); Gateway allowlist; T-004; erasure; INTENT.md:63 exclusion to lift via ADR.
2. **Accepted inferences:**
   - Separate `provider_showcase` collection.
   - Two-step upload (bytes → hash, then metadata).
   - Non-sequential 6-char code over a 31-symbol alphabet.
   - Content-free `/go`.
   - ZXing.Net.Maui for scanning and QRCoder for generation (pending approval).
   - Azurite locally; managed identity in the cloud.
   - A stream-based authenticated image loader.
3. **Key consequences:**
   - 7 new mobile views, 1 rewired entry point, and a Home first-run card.
   - Provider.Core showcase handlers plus media and `/go` modules.
   - Two Gateway rows.
   - Storage infrastructure plus an unconditional Cloud parameter.
   - Privacy policy update.
   - Two ADRs plus an INTENT update.
   - Bruno `9-Showcase` and OpenAPI regeneration.
4. **Risks and unknowns:**
   - User-generated-content moderation (App Review 1.2).
   - Store URLs not yet known (`1hk.14`), so they must come from configuration.
   - Orphan blobs.
   - Unknown-UA default page.
   - Poor-network uploads.
   - The image-library license.
5. **Resolved conflicts:**
   - No deferred deep linking; code plus Home card instead.
   - Export uses an existing graphics stack, no new package.
   - Image library deferred to the package-approval escalation.
6. **User escalation answers:** none in this meeting. The image-library license question is carried to package approval.
7. **Design priorities:** (1) upload safety pipeline, (2) content-free anonymous surface, (3) the customer's first 10 seconds, (4) targeted atomic writes plus complete erasure, (5) the authenticated image loader.

## Escalations

None answered during the meeting. Carried forward: server-side image library choice (license), to be asked alongside all new-package approvals.
