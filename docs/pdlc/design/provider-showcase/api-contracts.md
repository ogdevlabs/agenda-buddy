# API Contracts — F-037 Provider Showcase

All routes are served by the **Provider service** and reached through the Gateway via three new allowlist rows, each pointing to the `provider` cluster:

| Gateway prefix | Routes |
|---|---|
| `api/v1/showcase/**` | authoring, viewing, lookup, report, hide |
| `api/v1/media/**` | upload and fetch |
| `api/v1/go/**` | anonymous store redirect |

**Shared conventions**

- **Envelope:** success bodies use the service's own `DataResponse<T>` envelope (`{success, data, errors}`).
- **Errors:**
  - `400` is `ValidationProblem` (RFC 7807 with an `errors` map).
  - `401` comes from the JWT middleware, with no body.
  - `403` is mapped centrally from `ForbiddenException`.
  - `404` has a `ProblemDetails` body whose `type` is `showcase-not-found`.
  - `409` has a `ProblemDetails` body carrying a `code` extension.
  - `500` comes from `AgendaBuddyExceptionHandler`.
- **Addresses:** no route carries an email address in its path or query (T-004).
- **Auth:** "Provider" means the JWT `role` is `Provider`, and the route acts on the caller's own `sub`. Nothing in the request can name another account.

---

## 1. Media

### 1.1 `POST /api/v1/media` — upload an image

- **Auth:** JWT, role Provider.
- **Request:** the raw image as the body (not multipart). `Content-Type: image/jpeg | image/png | image/webp`, although the server ignores this header and sniffs magic bytes. Kestrel refuses a body over 5 MB + 1 KB before it is buffered.
- **201 Created:**
```json
{ "success": true, "data": { "hash": "9f2c…e1", "width": 1600, "height": 1067, "deduplicated": false } }
```
- **200 OK:** the same body with `"deduplicated": true`, when this provider already holds identical bytes.
- **400** with `errors.image`:

  | Code | Cause |
  |---|---|
  | `unsupported-format` | magic bytes are not JPEG, PNG or WebP |
  | `too-large` | over 5 MB |
  | `too-many-pixels` | over 40 MP |
  | `dimension-exceeded` | a side over 8000 px |
  | `undecodable` | the bytes fail to decode |

  Each code comes with a human-readable message.
- **401 / 403:** no token, or the caller is not a Provider.
- **413:** body over the Kestrel limit.
- **429:** over 60 uploads in one hour (`Retry-After` header).
- **503 `storage-unavailable`:** the blob store is unreachable. Nothing is recorded.

### 1.2 `GET /api/v1/media/{providerRef}/{hash}/{variant}` — fetch an image

- **Auth:** any valid JWT (accepted risk, ADR-069 / threat-model.md).
- **Path:**
  - `providerRef`: a 24-hex ObjectId.
  - `hash`: 64 lowercase hex characters.
  - `variant`: `full` or `thumb`.

  Anything else is `404`, and the blob store is never asked.
- **200:** `image/jpeg` bytes with these headers:
  - `Cache-Control: private, max-age=31536000, immutable`
  - `ETag: "{hash}-{variant}"`
  - `X-Content-Type-Options: nosniff`
  - `Content-Disposition: inline`
- **304:** returned on a matching `If-None-Match`.
- **404:** the image is unknown, detached, or belongs to an erased, deactivated or hidden provider. All of these share one body.
- **503 `storage-unavailable`.**

Example: `GET /api/v1/media/66f1a2b3c4d5e6f708192a3b/9f2c…e1/thumb` → `200 image/jpeg`.

---

## 2. Provider authoring (`/api/v1/showcase/me`)

Every route here requires **JWT, role Provider**, and acts on the caller's own showcase. On first use the showcase document is created implicitly.

### 2.1 `GET /api/v1/showcase/me` — the provider's own showcase

- **200:**
```json
{ "success": true, "data": {
  "providerRef": "66f1…3b", "publicCode": "K7Q2X9",
  "tagline": "Fine-line tattoos, Guadalajara", "about": "…",
  "photoHash": "…", "logoHash": null,
  "portfolio": [ { "hash": "…", "caption": "Botanical sleeve", "serviceId": "…", "width": 1600, "height": 1200, "addedAt": "2026-09-30T18:02:11Z" } ],
  "completeness": { "done": 3, "total": 5, "missing": ["logo", "about"] },
  "funnel": { "scans": 41, "opened": 17, "booked": 4, "windowDays": 7 }
} }
```
- **Completeness items:** photo, logo, tagline, about, and portfolio (at least 3 images).
- **Funnel fields:** `scans` is the sum of the `go_counters` platform counts; `opened` counts visits whose first source is scan or code; `booked` counts bookings within 7 days of that first visit.
- **`publicCode`** is `null` until §2.7 is called.
- **401 / 403.**

### 2.2 `PUT /api/v1/showcase/me/text` — set tagline and About

- **Request:** `{ "tagline": string|null, "about": string|null }`. Both are always written, and `null` clears a field.
- **Validation:** tagline is at most 80 grapheme clusters, about at most 600. Surrounding whitespace is trimmed, and a value that is empty after trimming is stored as null.
- **200:** the §2.1 body.
- **400** with `errors.tagline` or `errors.about`. **401 / 403.**

### 2.3 `PUT /api/v1/showcase/me/photo` and `PUT /api/v1/showcase/me/logo`

- **Request:** `{ "hash": string|null }`. `null` removes the image; for the photo, the avatar then falls back to the `AvatarCatalog` mark.
- **200:** the §2.1 body.
- **400:** `errors.hash` = `unknown-media` when the hash is not one of this provider's `media_refs`. A hash that is merely unknown is never stored, for the same reason as avatars.
- **401 / 403.**

### 2.4 `POST /api/v1/showcase/me/portfolio` — add an image

- **Request:** `{ "hash": string, "caption": string|null, "serviceId": string|null }`.
- **201 Created:** the new item. `Location` is `/api/v1/showcase/me/portfolio/{hash}`.
- **200:** the hash is already in the portfolio (idempotent retry); returns the existing item.
- **400:**
  - `unknown-media`
  - `caption` over 140 grapheme clusters
  - `serviceId` not one of the provider's services
- **409 `portfolio-full`:** "Your portfolio already has 20 images. Remove one to add another."
- **401 / 403.**

### 2.5 `PATCH /api/v1/showcase/me/portfolio/{hash}` — caption and service link

- **Request:** `{ "caption": string|null, "serviceId": string|null }`. Both are always written.
- **200:** the item. **400:** as in §2.4. **404:** the hash is not in the portfolio. **401 / 403.**

### 2.6 `DELETE /api/v1/showcase/me/portfolio/{hash}`

- **204:** returned whether or not the image was present, so the route is idempotent.
- **401 / 403.**

### 2.7 `PUT /api/v1/showcase/me/portfolio/order` — reorder

- **Request:** `{ "hashes": string[] }`, which must be a permutation of the current portfolio.
- **200:** the §2.1 body. Sending the current order is also `200`.
- **409 `portfolio-changed`:** the list does not match the current set, for example after an edit on another device. The client reloads and reapplies the change.
- **401 / 403.**

### 2.8 `POST /api/v1/showcase/me/code` — get or create the public code

- **Request:** no body.
- **200:** `{ "code": "K7Q2X9", "url": "https://{Showcase:Go:BaseUrl}/api/v1/go/K7Q2X9" }`. The same code is returned on every call.
- **401 / 403.**
- **500:** five successive collisions. This is practically unreachable: roughly 887 million codes are possible.

---

## 3. Customer viewing

### 3.1 `GET /api/v1/showcase/{providerRef}?source={source}` and `GET /api/v1/showcase/by-code/{code}?source={source}`

- **Auth:** any valid JWT.
- **`source`:** one of `scan`, `code`, `directory`, `appointment`, `message` or `booking`. If omitted, it is recorded as `directory`; an unknown value is recorded as `directory` rather than rejected.
- **`code` matching:** case-insensitive, and trimmed first.
- **200:**
```json
{ "success": true, "data": {
  "providerRef": "66f1…3b", "firstName": "Mariana", "lastName": "Ruiz",
  "professions": ["Tattoo Artist"],
  "tagline": "…", "about": "…", "photoHash": "…", "logoHash": "…", "avatarId": "geo-07",
  "portfolio": [ { "hash": "…", "caption": "…", "serviceId": "…", "width": 1600, "height": 1200, "isNew": true } ],
  "services": [ { "id": "…", "name": "Fine-line piece", "fee": 1800, "feeType": "Fixed", "durationMinutes": 120 } ],
  "relationship": {
    "isSelf": false, "isSubscribed": true,
    "nextAppointment": { "identifier": "…", "scheduledAt": "2026-10-12T16:00:00Z", "serviceName": "Fine-line piece" },
    "hasBookedBefore": true
  }
} }
```
- **What the body never contains:** the provider's email, phone number, appointments or customers. `services` lists only active, profession-classified services.
- **`isNew`:** true when `addedAt` is later than the caller's previous `last_seen_at`. It is false on a first visit, when everything is new.
- **`relationship.isSelf`:** true for Preview-as-customer, which also records no visit.
- **404 `showcase-not-found`:** "This provider isn't available". One body covers four cases:
  - the provider is unknown;
  - the provider is deactivated;
  - the provider is erased;
  - the caller has hidden this provider.

  A provider who exists but has no showcase document still answers `200`, with empty content, the designed empty state.
- **401.**
- **429:** `by-code` is limited per user to 30 lookups per minute, a code-enumeration brake that applies to signed-in users too.

### 3.2 `POST /api/v1/showcase/lookup` — avatar resolution for email-keyed screens

- **Auth:** any valid JWT.
- **Request:** `{ "emails": string[] }`, at most 50. Only the caller's own counterparties resolve:
  - for a Customer, providers they have an appointment with or are subscribed to;
  - for a Provider, only their own address.

  Any other address is silently omitted, which is what prevents address → `providerRef` enumeration.
- **200:** `[{ "email": "…", "providerRef": "…", "photoHash": "…"|null, "portfolioChangedAt": "…"|null }]`.
- **400:** more than 50 addresses. **401.**

### 3.3 `POST /api/v1/showcase/{providerRef}/report`

- **Auth:** any valid JWT; a provider cannot report their own showcase.
- **Request:** `{ "reason": "inappropriate|not_their_work|spam|other", "detail": string|null, "portfolioHash": string|null }`. `detail` is at most 500 characters and required when the reason is `other`.
- **202 Accepted:** the report is stored and the operator email is sent best-effort. A duplicate within 24 hours also returns `202`, without writing.
- **400:** reason or detail invalid. **404:** as §3.1. **401.**

### 3.4 `PUT /api/v1/showcase/{providerRef}/hide` and `DELETE /api/v1/showcase/{providerRef}/hide`

- **Auth:** JWT, role Customer.
- **Request:** no body.
- **204:** returned by both verbs, idempotently. A hidden provider disappears from the caller's directory and answers `404` on §3.1 and §1.2.
- **Unhiding:** done from More → Hidden providers, which lists them via `GET /api/v1/showcase/hidden` → `[{providerRef, firstName, lastName}]`.
- **401 / 403.**

---

## 4. Anonymous store redirect

### 4.1 `GET /api/v1/go/{code}`

- **Auth:** **none**. This is the feature's only anonymous route.
- **Behaviour:** identical for known, unknown and malformed codes, apart from the counter write.
  - **iOS browser identifier** (`iPhone|iPad|iPod`, or `Macintosh` with a touch hint): `302 Location: {Showcase:Go:AppStoreUrl}`.
  - **Android browser identifier:** `302 Location: {Showcase:Go:PlayStoreUrl}`.
  - **Otherwise, or when the matching store URL is not configured:** `200 text/html`, a static, fixed, provider-free "Get AgendaMe" page linking whichever store URLs are configured. Its bytes are the same for every code.
  - **Headers on every response:**
    - `Cache-Control: no-store`
    - `Referrer-Policy: no-referrer`
    - `X-Content-Type-Options: nosniff`
    - `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'`
- **Counter:** a known code gets `$inc go_counters.{ios|android|other}`. Nothing else is written, for any code.
- **429:** the per-IP window is exceeded (30 requests per minute, when `Security:RateLimiting:Enabled`). The body is plain text.
- **There is no other status.** A store URL is never taken from the request, so the route can't act as an open redirect.

Example: `GET /api/v1/go/K7Q2X9`, `User-Agent: …iPhone OS 18_0…` → `302 Location: https://apps.apple.com/app/id…`.

---

## 5. Modified routes

| Route | Change |
|---|---|
| `GET /api/v1/providers` | each `ProviderSummary` gains `providerRef`, `photoHash` and `portfolioChangedAt`; providers the caller has hidden are excluded. Additive, so older clients are unaffected |
| `DELETE /api/v1/providers/{email}` and `DELETE /api/v1/customers/{email}` | the erasure scope grows (ARCHITECTURE §3.4); the contract is unchanged, still `204` either way |

## 6. Pagination and rate limits

| Limit | Scope | Status |
|---|---|---|
| `/go` 30 per minute | per IP, config-gated | 429 |
| `/showcase/by-code` 30 per minute | per `sub` | 429 |
| `POST /media` 60 per hour | per provider | 429 |
| Report: one per reporter per provider per 24h | per pair | 202, no-op |
| `lookup` at most 50 addresses | per request | 400 |

The portfolio is at most 20 items, so no route needs pagination.

## 7. Documentation obligations

- **Bruno:** a `bruno/agenda-buddy/9-Showcase/` folder with one request per route above, in both environments.
- **OpenAPI:** regenerate the baselines with `REGENERATE_OPENAPI_BASELINES=1 … --filter FullyQualifiedName~OpenApiSpecBaselineWriter`, never with the script.
- **Gateway:** a structural test asserting the three rows exist, and that `go` is the only prefix reachable without a token.
