# OpenAPI specs

Derived from the committed `*.json` specs in this directory (2026-09-08).
**Regenerate any time** — this directory is a build artifact, not a hand-maintained document.

> ⚠️ The specs themselves come from `REGENERATE_OPENAPI_BASELINES=1 dotnet test AgendaBuddy.IntegrationTests --filter FullyQualifiedName~OpenApiSpecBaselineWriter`, **not** from
> `scripts/generate-openapi.sh` — that script reformats with 4-space indentation while the committed
> files are 2-space `OpenApiJsonWriter` output, so running it rewrites all 7 with the wrong bytes and
> fails `OpenApiSpecDriftTest` for every service at once. This table is read back out of the committed
> specs, so it cannot disagree with them.

| Service | Standalone port | Spec | Paths |
|---|---|---|---|
| Provider | 6030 | [`Provider.json`](Provider.json) | 8 |
| Services | 6031 | [`Services.json`](Services.json) | 2 |
| Calendar | 6032 | [`Calendar.json`](Calendar.json) | 5 |
| Booking | 6033 | [`Booking.json`](Booking.json) | 8 |
| Customer | 6034 | [`Customer.json`](Customer.json) | 13 |
| Profession | 6035 | [`Profession.json`](Profession.json) | 4 |
| Identity | 6036 | [`Identity.json`](Identity.json) | 9 |

## Every route

### Provider

- `GET    /api/v1/providers`
- `POST   /api/v1/providers`
- `GET    /api/v1/providers/{email}`
- `PUT    /api/v1/providers/{email}`
- `DELETE /api/v1/providers/{email}`
- `PUT    /api/v1/providers/{email}/work-hours`
- `PUT    /api/v1/providers/{email}/work-week`
- `GET    /api/v1/providers/{email}/report`
- `POST   /api/v1/providers/{email}/deactivate`
- `PUT    /api/v1/providers/{email}/avatar`
- `PUT    /api/v1/providers/{email}/consent`

### Services

- `GET    /api/v1/services/{email}`
- `PUT    /api/v1/services/{email}`
- `PATCH  /api/v1/services/{email}`
- `DELETE /api/v1/services/{email}/{name}`

### Calendar

- `GET    /api/v1/calendar/availability/{email}`
- `GET    /api/v1/calendar/appointments/{email}`
- `GET    /api/v1/calendar/blocks/{email}`
- `POST   /api/v1/calendar/blocks/{email}`
- `GET    /api/v1/calendar/blocks/{email}/conflicts`
- `DELETE /api/v1/calendar/blocks/{email}/{identifier}`

### Booking

- `PUT    /api/v1/booking/appointments`
- `POST   /api/v1/booking/appointments`
- `DELETE /api/v1/booking/appointments`
- `POST   /api/v1/booking/appointments/{identifier}/status`
- `POST   /api/v1/booking/appointments/{identifier}/reschedule`
- `POST   /api/v1/booking/appointments/{identifier}/reschedule-request`
- `POST   /api/v1/booking/appointments/{identifier}/reschedule-answer`
- `GET    /api/v1/booking/appointments/{identifier}/notes`
- `POST   /api/v1/booking/appointments/{identifier}/notes`
- `PUT    /api/v1/booking/notes/{id}`
- `DELETE /api/v1/booking/notes/{id}`
- `GET    /api/v1/booking/appointments/{identifier}/payment`
- `POST   /api/v1/booking/appointments/{identifier}/payment`

### Customer

- `GET    /api/v1/customers`
- `POST   /api/v1/customers`
- `GET    /api/v1/customers/{email}`
- `PUT    /api/v1/customers/{email}`
- `DELETE /api/v1/customers/{email}`
- `POST   /api/v1/customers/{email}/subscriptions/{providerEmail}`
- `DELETE /api/v1/customers/{email}/subscriptions/{providerEmail}`
- `GET    /api/v1/customers/{email}/subscriptions`
- `PUT    /api/v1/customers/{email}/avatar`
- `PUT    /api/v1/customers/{email}/consent`
- `GET    /api/v1/messages`
- `POST   /api/v1/messages`
- `GET    /api/v1/messages/thread/{counterpartEmail}`
- `POST   /api/v1/messages/{id}/read`
- `GET    /api/v1/notifications`
- `GET    /api/v1/notifications/unread-count`
- `POST   /api/v1/notifications/{id}/read`
- `POST   /api/v1/notifications/read-all`

### Profession

- `GET    /api/v1/professions`
- `GET    /api/v1/professions/{name}`
- `GET    /api/v1/professions/providers/{email}`
- `PUT    /api/v1/professions/providers/{email}`
- `DELETE /api/v1/professions/providers/{email}/{name}`

### Identity

- `POST   /device-token`
- `DELETE /device-token`
- `POST   /api/v1/auth/register`
- `POST   /api/v1/auth/login`
- `POST   /api/v1/auth/refresh`
- `POST   /api/v1/auth/logout`
- `POST   /api/v1/auth/password-reset/request`
- `POST   /api/v1/auth/password-reset/confirm`
- `DELETE /api/v1/auth/account`
- `POST   /api/v1/auth/register/confirm`
