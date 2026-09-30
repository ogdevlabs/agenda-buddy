# Threat Model — provider-showcase (F-037)
<!-- pdlc-template-version: 1.0.0 -->

**Triage:** Full
**Convened:** 2026-09-30
**Lead:** Phantom (Security Reviewer)
**Participants:** Phantom, Neo, Bolt, Echo, Pulse, Atlas, Muse, Jarvis, Friday (solo-mode party; MOM at `docs/pdlc/mom/MOM_threat-model_provider-showcase_2026-09-30.md`)
**Status:** Pending human approval (Step 12)

Threat IDs use the `T-37x` range so they cannot collide with the project's existing threat IDs (T-004, T-203, T-208, T-302).

---

## Triage Record

| Question | Answer | Evidence |
|---|---|---|
| Trust boundary introduced/modified? | **yes** | First anonymous route (`/go`), ARCHITECTURE §2/§4. Provider service gains an egress to Azure Blob via managed identity (§7). User-supplied binary content crosses into the backend for the first time (§3.1) |
| Regulated data? | **yes** | PII: face photos (biometric-adjacent), images that may contain third parties, EXIF GPS location in raw uploads, visit records linking customers to providers. Erasure obligations (App Review 5.1.1(v)) |
| New attack surface? | **yes** | Binary upload + image parser, anonymous redirect, 14 new endpoints, email→ref lookup, QR scanner (mobile handler), UGC visible to other users |

**Triage outcome:** Full (3/3)

---

## Trust Boundaries

| ID | Boundary | What crosses | Trust direction | Diagram reference |
|---|---|---|---|---|
| TB-1 | Anonymous internet → Gateway → `/go` | code path segment, User-Agent, source IP | untrusted → semi-trusted | ARCHITECTURE §3.2 (Go node) |
| TB-2 | Signed-in mobile client → Gateway → Provider API | JWT (passthrough), JSON bodies, **raw image bytes** | untrusted (authenticated) → semi-trusted | §3.1, §3.2 |
| TB-3 | Provider API → `ImagePipeline` (native SkiaSharp decoder) | attacker-controlled bytes into native code | semi-trusted → trusted process memory | §2 |
| TB-4 | Provider API → Azure Blob (managed identity) / Azurite | re-encoded images, keys | trusted → trusted external (egress) | §7 |
| TB-5 | Provider A's content → Customer B's screen | images, captions, tagline, about (UGC) | untrusted content → other users | §3.3 |
| TB-6 | Printed / posted QR → in-app scanner | arbitrary QR payload | untrusted → client | ARCHITECTURE §8 |
| TB-7 | Provider API → operator email (report) | reporter-supplied text | semi-trusted → external inbox | api-contracts §3.3 |

---

## Threats Identified

### T-371: Decompression bomb or parser exploit through upload

- **STRIDE category:** Denial of Service; Elevation of Privilege
- **Trust boundary:** TB-2 → TB-3
- **Asset affected:** Provider service availability (which also hosts the whole provider directory), and process integrity
- **Attack vector:** A provider account uploads a 5 MB PNG that declares 60000×60000 pixels, or a crafted WebP that targets a native decoder CVE. A full decode would allocate around 14 GB and kill the replica. With a CVE, it could give code execution inside the container.
- **Severity:** HIGH
- **DREAD breakdown:** Damage H · Reproducibility H · Exploitability M (needs a provider account, which is free to register) · Affected users: all providers and customers of that replica · Discoverability M
- **Mapped frameworks:** OWASP API4:2023 (Unrestricted Resource Consumption); CWE-409; CWE-787
- **Current mitigation status:** None (no upload exists today)
- **Proposed action:** Mitigate now
  - **Controls, in order:**
    1. The Kestrel `RequestSizeLimit` refuses the body before buffering.
    2. Magic-byte sniff.
    3. `SKCodec` header dimensions checked **before** any pixel allocation (≤8000 px per side, ≤40 MP).
    4. First frame only.
    5. A `SemaphoreSlim(2)` per replica bounds concurrent decodes.
  - SkiaSharp native assets are covered by `dotnet list package --vulnerable` and Trivy in CI.
  - **Cost and fit:** Bolt estimates 0.5 day inside the `ImagePipeline` task. Neo confirms the pipeline is pure and sits on the only path that accepts bytes.
  - **Testable acceptance criterion:** Given a 40 KB PNG whose header declares 20000×20000, `POST /api/v1/media` responds `400 dimension-exceeded`, and the process working set grows by less than 50 MB during the request.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).
- **Cross-talk note:** Pulse pointed out that `/media` shares a replica with the whole directory. That made a single bad upload an availability threat for customers who never touch the feature, and moved the rating from MEDIUM to HIGH.

### T-372: Location leak through EXIF/GPS metadata

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-2 → TB-5
- **Asset affected:** Provider physical safety. Many independent providers work from home, and a phone photo of their work carries the home's GPS coordinates.
- **Attack vector:** A provider uploads a studio photo straight from the camera roll. Any customer fetches `full`, reads EXIF GPS, and gets a home address.
- **Severity:** HIGH
- **DREAD breakdown:** Damage H (physical safety) · Reproducibility H · Exploitability H (any EXIF viewer) · Affected users: every provider · Discoverability H
- **Mapped frameworks:** CWE-359; OWASP API3:2023
- **Current mitigation status:** Designed out by D-2 (every image is re-encoded, and only the output is stored)
- **Proposed action:** Mitigate now (verify with a test)
  - **Testable acceptance criterion:** Given a JPEG fixture carrying GPS EXIF, an XMP packet and an ICC profile, the `full` and `thumb` variants fetched from `GET /api/v1/media/...` contain no APP1 (EXIF/XMP) segment and no GPS tag.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).
- **Cross-talk note:** Muse added that the *client* conversion (HEIC → JPEG) must not be relied on. An older app build, or a direct API call, skips it, which is why the server re-encode is the control.

### T-373: IDOR on authoring, or attaching another provider's media

- **STRIDE category:** Elevation of Privilege; Tampering
- **Trust boundary:** TB-2
- **Asset affected:** Integrity of every showcase
- **Attack vector:**
  - **(a)** Provider A calls an authoring route with B's identity.
  - **(b)** A takes a hash visible on B's showcase and attaches it as A's own portfolio image or logo. This is image theft that also makes A's showcase depend on B's blob.
  - **(c)** A Customer calls authoring routes.
- **Severity:** HIGH
- **DREAD breakdown:** Damage M · Reproducibility H · Exploitability H · Affected users: any provider · Discoverability H (the hashes are in every response)
- **Mapped frameworks:** OWASP API1:2023 (BOLA); API5:2023 (BFLA); CWE-639
- **Current mitigation status:** Designed. Authoring routes are `/showcase/me/*`, keyed by the JWT `sub`, with no identifier in the path. Attach requires a `media_refs` record for `(caller's providerId, hash)`. Role `Provider` is asserted.
- **Proposed action:** Mitigate now
  - **Testable acceptance criterion:**
    - Given provider A and a hash present only in provider B's `media_refs`, `POST /api/v1/showcase/me/portfolio {hash}` as A responds `400 unknown-media`, and A's portfolio is unchanged.
    - A Customer JWT on any `/showcase/me/*` route responds `403`.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).
- **Cross-talk note:** Bolt found (b) while Neo was confirming per-provider key prefixes. Because the key prefix already carries the provider id, B's blob can't even be *resolved* under A's prefix. The ownership check turns that into a clean 400 instead of a broken image.

### T-374: Incomplete erasure leaves images or visit records behind

- **STRIDE category:** Information Disclosure (regulatory)
- **Trust boundary:** TB-4
- **Asset affected:** Erased user's face photo, work and relationships; App Review 5.1.1(v) compliance
- **Attack vector:**
  - Erasure deletes the showcase document, but the blob delete fails and nothing retries it.
  - A customer's own `showcase_visits` and `showcase_blocks` rows are forgotten because the erasure set only lists provider-side collections.
- **Severity:** HIGH
- **DREAD breakdown:** Damage H (a compliance failure, and the photo is identifying) · Reproducibility M · Exploitability L (not attacker-driven) · Affected users: every erased account · Discoverability L
- **Mapped frameworks:** GDPR Art. 17 analogue; Mexico LFPDPPP ARCO cancellation; App Review 5.1.1(v)
- **Current mitigation status:** Designed (ARCHITECTURE §3.4): delete the document first, then the prefix; the sweep's second clause catches failures; `AddAccountErasure` is extended as a set.
- **Proposed action:** Mitigate now
  - **Testable acceptance criterion:**
    - Given a provider with a photo and 3 portfolio images, after `DELETE /api/v1/providers/{email}`, every one of the account's `GET /api/v1/media/...` URLs responds `404`, and the provider's rows in `provider_showcase`, `media_refs`, `showcase_visits`, `showcase_reports`, `showcase_blocks` and `go_counters` number zero.
    - With `IBlobStore.DeletePrefixAsync` made to throw, the route still responds `204`, and one `MediaSweepService` pass leaves `ListAsync("{providerId}/")` empty.
    - Erasing a *customer* removes that customer's `showcase_visits` and `showcase_blocks`.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).

### T-375: Illegal or abusive content in showcases

- **STRIDE category:** Information Disclosure (harmful content to other users); legal exposure
- **Trust boundary:** TB-5
- **Asset affected:** Customers, the product's App Store standing (Guideline 1.2), and the operator's legal position
- **Attack vector:** A provider uploads explicit, violent, infringing or illegal material. At the extreme, that could include child sexual abuse material. Customers who scan or browse see it.
- **Severity:** HIGH
- **DREAD breakdown:** Damage H · Reproducibility H · Exploitability H · Affected users: any customer who views it · Discoverability H
- **Mapped frameworks:** App Store Review Guideline 1.2 (UGC: filter, report, block, contact); US 18 U.S.C. §2258A (for US-served content)
- **Current mitigation status:** Partial, by design. Report (reasons, operator email), Hide this provider, a Terms clause and a contact address (PRD R31–R33). Content is only visible to signed-in users, which limits reach.
- **Proposed action:** **Mitigate now** for the App Review 1.2 minimum, plus **Mitigate later** for automated scanning.
  - **Now:** Report and Hide as designed, plus an operator **takedown lever** so a reported showcase can be pulled within the Guideline's expectation without a deploy. The lever is a `hidden_by_operator` flag on `provider_showcase`: set by a direct database write in this feature, it makes the showcase and its media answer 404 to everyone except the owner.
  - **Later (ADR required):** automated hash-matching (e.g. PhotoDNA or a cloud content-safety API) at upload. It depends on a vendor and the legal questions below.
  - **Testable acceptance criterion:**
    - Given a showcase with `hidden_by_operator: true`, `GET /api/v1/showcase/{ref}` and every one of its `GET /api/v1/media/...` URLs respond `404` to a customer, and `200` to the owner via `/showcase/me`.
    - `POST /report` writes one `showcase_reports` row and calls `IEmailSender` once, with the provider ref and reason.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).
- **Cross-talk note:** Atlas and Muse asked for the takedown lever. Without it, a Report lands in an inbox with no action the operator can take short of erasing the account, and Apple reviewers ask what happens *after* a report.

### T-376: Email-to-provider-ref lookup as an enumeration oracle

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-2
- **Asset affected:** Whether an email address belongs to a provider (account enumeration), and their photo
- **Attack vector:** A signed-in user posts 50 guessed addresses per call to `/showcase/lookup` and learns which are providers.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M · Reproducibility H · Exploitability H · Affected users: all providers · Discoverability M
- **Mapped frameworks:** OWASP API3:2023; CWE-204
- **Current mitigation status:** Designed. Only the caller's own counterparties resolve (via an appointment or a subscription); everything else is silently omitted.
- **Proposed action:** Mitigate now
  - **Testable acceptance criterion:** Given a customer with no appointment or subscription with provider P, `POST /api/v1/showcase/lookup {emails:[P]}` returns an empty array, the same response as for a non-existent address.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).

### T-377: Public-code enumeration

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-1, TB-2
- **Asset affected:** A browsable directory of every provider's showcase
- **Attack vector:**
  - **Anonymously:** walk `/go/{code}` looking for any difference between hits and misses.
  - **Signed in:** walk `/showcase/by-code/{code}`.
- **Severity:** MEDIUM. The directory is already browsable by any signed-in user, so the marginal disclosure is small. The main concern is the anonymous oracle.
- **DREAD breakdown:** Damage L-M · Reproducibility H · Exploitability M · Affected users: all providers · Discoverability H
- **Mapped frameworks:** OWASP API4:2023; CWE-204; CWE-208
- **Current mitigation status:** Designed. `/go` gives an identical response for known and unknown codes (the counter write is the only difference, and it is invisible); `by-code` is limited to 30/min per `sub`; there are ~887M possible codes.
- **Proposed action:** Mitigate now
  - **Testable acceptance criterion:**
    - For the same User-Agent, `GET /api/v1/go/{known}` and `GET /api/v1/go/{unknown}` return byte-identical status, headers (excluding `Date`) and body.
    - The 31st `by-code` request within a minute from one `sub` responds `429`.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).
- **Cross-talk note:** Echo noted that a timing difference remains, because a known code costs one more write. Phantom rated it LOW given the rate limit, and the party chose not to add an artificial delay.

### T-378: Open redirect through `/go`

- **STRIDE category:** Spoofing (phishing via a trusted domain)
- **Trust boundary:** TB-1
- **Asset affected:** Brand trust; users sent to a phishing page from an AgendaMe URL
- **Attack vector:** A future change adds `?to=` or reads the store URL from the showcase document. The trusted `/go` host then forwards anywhere.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M · Reproducibility H · Exploitability H (if introduced) · Affected users: anyone who scans · Discoverability H
- **Mapped frameworks:** CWE-601; OWASP A01:2021
- **Current mitigation status:** Designed. `Location` comes only from `Showcase:Go:*` configuration.
- **Proposed action:** Mitigate now (a regression guard)
  - **Testable acceptance criterion:** `GET /api/v1/go/{code}?to=https://evil.example&url=https://evil.example` returns a `Location` equal to the configured store URL, and no response header or body contains `evil.example`.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).

### T-379: Storage-cost exhaustion through upload spam

- **STRIDE category:** Denial of Service (financial)
- **Trust boundary:** TB-2 → TB-4
- **Asset affected:** Azure spend, blob container size
- **Attack vector:** Scripted free provider accounts upload unattached images in a loop. Each stays up to 24 hours before the sweep removes it.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M · Reproducibility H · Exploitability M · Affected users: operator · Discoverability M
- **Mapped frameworks:** OWASP API4:2023
- **Current mitigation status:** Designed. 60 uploads per hour per provider, the 24h sweep, and ≤~1 MB stored per image after re-encoding.
- **Proposed action:** Mitigate now. The per-provider limit is the control; a storage budget alert in Azure (Pulse, infrastructure) is the backstop.
  - **Testable acceptance criterion:** The 61st `POST /api/v1/media` by one provider within an hour responds `429` with `Retry-After`, and writes no blob.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).

### T-380: Visit records expose which customers looked at a provider

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-5 (reverse direction)
- **Asset affected:** Customer privacy. Browsing a therapist's showcase, for example, is itself sensitive.
- **Attack vector:** The provider funnel, or a future feature, lists *who* viewed the showcase.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M-H (sensitive professions) · Reproducibility H · Exploitability L (it needs a feature to expose it) · Affected users: all customers · Discoverability M
- **Mapped frameworks:** CWE-359
- **Current mitigation status:** Designed. The funnel returns aggregate counts only, and no route returns `showcase_visits` rows. The Privacy Policy names visit records.
- **Proposed action:** Mitigate now (a contract guard)
  - **Testable acceptance criterion:** The `GET /api/v1/showcase/me` response body contains no customer email or customer identifier. `funnel` holds integers only, asserted on a showcase with at least one recorded visit.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).

### T-381: Content sniffing or stored XSS through served media

- **STRIDE category:** Tampering; Information Disclosure
- **Trust boundary:** TB-5
- **Asset affected:** Any future web surface that renders media; tooling that opens the URLs
- **Attack vector:** A polyglot JPEG/HTML file is served and interpreted as HTML.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M · Reproducibility M · Exploitability L (every byte served is our own re-encoded output) · Affected users: low today · Discoverability M
- **Mapped frameworks:** CWE-434; CWE-79
- **Current mitigation status:** Designed. Re-encoding means served bytes are never the upload. `Content-Type: image/jpeg` is fixed, with `nosniff` and `Content-Disposition: inline`.
- **Proposed action:** Mitigate now (test only)
  - **Testable acceptance criterion:** Uploading a JPEG/HTML polyglot fixture either yields `400`, or a `full` variant whose bytes differ from the upload and contain no `<script` sequence. The response carries `X-Content-Type-Options: nosniff`.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).

### T-382: Provider denies uploading reported content

- **STRIDE category:** Repudiation
- **Trust boundary:** TB-2
- **Asset affected:** The operator's ability to act on reports and respond to legal requests
- **Attack vector:** A provider deletes an offending image after a report and claims it was never there.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M · Reproducibility H · Exploitability H · Affected users: operator · Discoverability L
- **Mapped frameworks:** CWE-778
- **Current mitigation status:** Partial. Every command audits via `eventStore.SaveAsync` (CONSTITUTION §3).
- **Proposed action:** Mitigate now. The audit event for attach, remove and set-photo/logo carries `{sub, providerId, hash, at}`, and a report stores the `portfolio_hash` it concerned.
  - **Testable acceptance criterion:** After `POST` and `DELETE` on `/api/v1/showcase/me/portfolio`, the event store holds two events for the provider, each carrying the hash and the actor's `sub`.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).

### T-383: Managed identity scoped wider than it needs

- **STRIDE category:** Elevation of Privilege
- **Trust boundary:** TB-4
- **Asset affected:** The storage account, which may later hold other containers
- **Attack vector:** A compromised Provider replica uses an account-wide `Storage Blob Data Contributor` role to read or write containers it has no business in.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M · Reproducibility L · Exploitability L (it needs a prior compromise) · Affected users: operator · Discoverability L
- **Mapped frameworks:** CWE-250; Azure least-privilege guidance
- **Current mitigation status:** None. Aspire's default role assignment is account-scoped.
- **Proposed action:** Mitigate now. A dedicated Storage account for media, holding only the `media` container, makes account scope equal container scope. `allowBlobPublicAccess=false` and shared-key access are disabled (managed identity only).
  - **Testable acceptance criterion:** The generated Bicep/manifest for the media storage account sets `allowBlobPublicAccess: false` and `allowSharedKeyAccess: false`, and the only role assignment on it targets the provider app's identity. Asserted by an `AgendaBuddy.AppHost.Tests` structural test.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).

### T-384: Signed-in users can fetch any provider's media

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-2
- **Asset affected:** Showcase images
- **Attack vector:** A signed-in user who knows a `providerRef` and hash can fetch the image without visiting the showcase, including after the provider hid them. The hide and takedown 404s cover the latter.
- **Severity:** LOW-MEDIUM
- **DREAD breakdown:** Damage L (the content is intended for display) · Reproducibility H · Exploitability M (it needs the hash from a prior response) · Affected users: providers · Discoverability M
- **Mapped frameworks:** OWASP API1:2023 (by design)
- **Current mitigation status:** Hidden, erased, deactivated and taken-down providers answer 404; hashes are 256-bit.
- **Proposed action:** **Accept.** Recorded in ADR-069 (D-8).
  - **Atlas's justification:** a showcase exists to be shown to prospective customers, and relationship-gated media would break Preview and the scan-first flow the feature exists for.
  - **Residual risk:** a device's cache keeps images up to 30 days after a takedown. That's acceptable, because the viewer can't reopen the showcase.
- **Decision (human, at Step 12 approval):** Approved as recommended (2026-09-30).

---

## Threats Noted but Not Prioritized

| ID | Title | STRIDE | Boundary | Why deprioritized |
|---|---|---|---|---|
| T-NL-1 | Malicious QR payload opened by the in-app scanner | Spoofing | TB-6 | The scanner never follows a URL; it pulls out a 6-char code or shows the inline "not an AgendaMe code" message. Guarded by `ShowcaseCodeParser` unit tests |
| T-NL-2 | Header or HTML injection in the operator report email | Tampering | TB-7 | Plain-text body; `detail` is encoded; subject is fixed; addresses come from config |
| T-NL-3 | `/go` unlimited when `Security:RateLimiting:Enabled` is off | DoS | TB-1 | Off only locally (ADR-033); the cloud graph turns it on; the route is a single-document `$inc` |
| T-NL-4 | Timing side channel on `/go` known vs unknown codes | Info disclosure | TB-1 | One extra indexed write; rate limit; small marginal value (see T-377) |
| T-NL-5 | `providerRef` (ObjectId) embeds a creation timestamp | Info disclosure | TB-2 | Reveals only when the account was created; not PII; already the `_id` everywhere |
| T-NL-6 | Blob container accidentally created public | Info disclosure | TB-4 | `EnsureContainerAsync` sets access `None`, and account-level public access is disabled (T-383) |

---

## Open Questions for Human

1. **Content-safety legal obligations.** Is AgendaMe distributed in the US App Store as well as Mexico? US distribution creates CSAM reporting obligations for any provider that becomes aware of such content. That decides whether T-375's automated scanning can stay "mitigate later", and which vendor.
2. **Report inbox.** Which address should `Showcase:ReportEmail` deliver to, and who acts on it, with what turnaround? App Review expects a timely response. The takedown lever in T-375 is a direct database write for now; is that acceptable operationally, or do you want an admin route (a new privileged surface, and a threat to model)?
3. **Face photos.** Are provider profile photos biometric data under your privacy posture? The design stores them only as images and does no face processing. Confirm the Privacy Policy wording ("profile photo") is enough pending the lawyer review already flagged in `LegalDocuments.cs`.
4. **Confirm ADR-069's accepted risk** (T-384): any signed-in user may fetch any showcase media by hash.

---

## Approval Outcomes (filled in at Step 12)

| Threat ID | Party recommendation | Human decision | Rationale |
|---|---|---|---|
| T-371 | Mitigate now | | Mitigate now (approved 2026-09-30) | As recommended |
| T-372 | Mitigate now | | Mitigate now (approved 2026-09-30) | As recommended |
| T-373 | Mitigate now | | Mitigate now (approved 2026-09-30) | As recommended |
| T-374 | Mitigate now | | Mitigate now (approved 2026-09-30) | As recommended |
| T-375 | Mitigate now (report/hide/takedown) + Mitigate later (scanning) | | Mitigate now (approved 2026-09-30) | As recommended |
| T-376 | Mitigate now | | Mitigate now (approved 2026-09-30) | As recommended |
| T-377 | Mitigate now | | Mitigate now (approved 2026-09-30) | As recommended |
| T-378 | Mitigate now | | Mitigate now (approved 2026-09-30) | As recommended |
| T-379 | Mitigate now | | Mitigate now (approved 2026-09-30) | As recommended |
| T-380 | Mitigate now | | Mitigate now (approved 2026-09-30) | As recommended |
| T-381 | Mitigate now | | Mitigate now (approved 2026-09-30) | As recommended |
| T-382 | Mitigate now | | Mitigate now (approved 2026-09-30) | As recommended |
| T-383 | Mitigate now | | Mitigate now (approved 2026-09-30) | As recommended |
| T-384 | Accept | Accept (approved 2026-09-30) | Promotional content; ADR-069 |

**ADR registry updates required:**
- ADR-069: image storage and serving, including the T-384 accepted risk
- ADR-072: automated content-safety scanning deferred (T-375, mitigate later)

**Tasks and security acceptance criteria to be created at Plan (Step 13):** one `[security]` AC per "mitigate now" threat above, using the criterion text as written. Each is attached to the task that owns the control:

| Threat | Owning task |
|---|---|
| T-371, T-372, T-381 | `ImagePipeline` |
| T-373, T-382 | authoring handlers |
| T-374 | erasure |
| T-375 | report/hide/takedown |
| T-376, T-377, T-380 | viewer/lookup |
| T-378 | `/go` |
| T-379 | upload rate limit |
| T-383 | AppHost storage |

---

## Revision History

| Date | Author | Change |
|---|---|---|
| 2026-09-30 | Phantom (initial draft) | Created at Step 10.5, Full triage, solo-mode party |
