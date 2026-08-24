# API Contracts — API Gateway and Mobile Contract (F-015)

**Date:** 2026-08-23 · **Feature ID:** F-015

**No new domain API endpoints.** This feature does not add or change any route on the seven backend
services — every `api/v1/...` route the gateway forwards to already exists exactly as F-014 (or an earlier
feature) shipped it. What is new is (1) the gateway's own routing/error contract, and (2) the corrected
client-side route table `MobileApp` must call. Both are documented below because Construction needs them as
the source of truth, even though neither is a new domain endpoint in the usual PRD sense.

---

## 1. Gateway contract

### `GET /health`, `GET /alive`

Same shape as all seven services (`AddServiceDefaults()`/`MapDefaultEndpoints()`) — `/health` includes a
liveness check against every destination it proxies to being reachable; `/alive` is a bare liveness probe.

### Any `api/v1/{service}/**` path

**Method:** whatever the destination route requires (passthrough, unchanged) · **Auth:** passthrough,
unmodified · **Request body:** passthrough, unmodified.

**Success response:** whatever the destination returns, byte-for-byte, with response headers passed
through.

**Error response — destination unreachable, timed out, or 5xx:**

```json
{
  "type": "https://agendabuddy.dev/errors/gateway-destination-unreachable",
  "title": "The service handling this request is unavailable",
  "status": 502,
  "detail": "The 'booking' service did not respond.",
  "failedService": "booking",
  "requestId": "..."
}
```

`failedService` names the YARP cluster the request was routed to (`booking`, `calendar`, `customer`,
`provider`, `services`, `profession`, `identity`) — the field the client's error-display logic (PRD AC 5)
reads to show "Booking is unavailable, try again" rather than a generic connectivity error. This is a
`ProblemDetails`-shaped body (RFC 7807), matching the six domain services' existing envelope rather than
inventing a third shape alongside the two that already coexist (see Known Risks in the PRD).

**Error response — no matching route:**

```json
{
  "type": "https://agendabuddy.dev/errors/gateway-no-route",
  "title": "No backend service matches this path",
  "status": 404,
  "detail": "No destination configured for 'api/v1/nonexistent/...'.",
  "requestId": "..."
}
```

Covers Edge Case #5 (stale client build or a route typo) — a clear 404 naming the problem, not a swallowed
or generic error.

---

## 2. Corrected client route table (`MobileApp`)

Every row corrects a call documented as broken in `docs/pdlc/context/16-mobile-client.md`. The base address
for all rows is the gateway's single discovered address (§3), not per-service ports.

| Client method | Before (broken) | After (F-015) | Verb | Notes |
|---|---|---|---|---|
| `BookingApiService.GetTodayAppointmentsAsync` | `GET booking?date=` (no matching route) | `GET api/v1/booking/appointments?from=&to=` | GET | Matches Booking's actual list route |
| `BookingApiService.GetAppointmentAsync` | `GET booking/{id}` (no matching route) | `GET api/v1/booking/appointments/{identifier}` | GET | |
| `BookingApiService.UpdateStatusAsync` | `PUT booking/{id}` with `{"status": "Confirmed"}` (ignored post-F-014; wrong payload shape) | `POST api/v1/booking/appointments/{identifier}/status` with `{"status": "Completed"}` | POST | F-014's dedicated transition route (PRD Requirement 6). Customer-facing UI must not offer this action at all — provider-only |
| `CalendarApiService.GetAvailabilityAsync` | `GET calendar?from=&days=` | `GET api/v1/calendar/availability/{email}?from=&days=` | GET | |
| `CalendarApiService.GetAppointmentsAsync` | *(wrong path — inference, not read directly)* | `GET api/v1/calendar/appointments/{email}` | GET | |
| `CustomerApiService.*` | *(inference — prefix-less, per 16-mobile-client.md)* | `api/v1/customers/...` | GET/POST/PUT | Corrected to the prefix every other client call needs |
| `MessagingApiService.*` | *(no backend route existed before F-014)* | `api/v1/messages/...` (send, inbox, thread, mark-read) | GET/POST | F-014's new top-level route group |
| `NotificationApiService.*` | *(no backend route existed before F-014)* | `api/v1/notifications/...` (list, mark-read) | GET/POST | F-014's new top-level route group. Empty list is the normal state (PRD Requirement 12) |
| `AuthService.LoginAsync` / `RegisterAsync` | `api/v1/auth/login` / `api/v1/auth/register` (already correct) | unchanged | POST | Already worked, by coincidence of sharing the old fallback host |
| `AuthService.RefreshAsync` *(new)* | *(never called)* | `POST api/v1/auth/refresh` | POST | Wired for the first time (PRD Requirement 9) |
| `AuthService.LogoutAsync` | clears local storage only | `POST api/v1/auth/logout` **and** clears local storage | POST | Server-side invalidation added (PRD Requirement 10) |
| `PushNotificationService.RegisterAsync` | `POST device-token` (already correct — root-mapped on Identity) | unchanged | POST | |
| *(new)* Provider report | *(not previously called)* | `GET api/v1/providers/{email}/report` | GET | F-014's new route. Renders `revenueUnavailableReason` when `revenueAvailable` is `false` (PRD Requirement 12) |
| *(new)* Provider deactivation | *(not previously called)* | `POST api/v1/providers/{email}/deactivate` | POST | F-014's new route |
| *(new)* Session notes | *(not previously called)* | `GET`/`PUT api/v1/booking/appointments/{identifier}/notes` | GET/PUT | F-014's new routes |
| *(new)* Payments | *(not previously called)* | `POST`/`GET api/v1/booking/appointments/{identifier}/payment` | GET/POST | F-014's new routes; client copy must not claim a `local_`-prefixed payment has been charged |

**Payload shape corrections:** every `POST`/`PUT` above sends the request body shape each corrected route's
`api-contracts.md` (F-014's, for the new routes; each service's existing DTOs for the corrected ones)
already documents — no new request/response shape is invented by this feature.

---

## 3. Rate limiting / pagination

Unchanged from what each destination service already enforces (F-021's login/register limiter; F-016's
pagination on list endpoints) — the gateway forwards headers and status codes unmodified, so a `429` from
Identity or a paginated `{items, totalCount, page, pageSize}` envelope from a list route reaches the client
exactly as the destination service produced it.
