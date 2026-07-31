# API Contracts: Mobile App (iOS + Android)
<!-- pdlc-template-version: 2.4.0 -->

**Feature:** mobile-app
**Date:** 2026-07-31
**Status:** Draft

---

## Summary

The mobile app is a consumer of existing backend endpoints. Only **one new endpoint** is introduced: device-token registration on the Identity service. All other endpoints used by the app already exist and are documented here for reference.

---

## New Endpoint

### `POST /identity/device-token`

Register or update a push notification device token for the authenticated user.

**Authentication:** Required — Bearer JWT (RS256)

**Request body:**

```json
{
  "token": "string",       // FCM registration token (Android) or APNs device token (iOS). Required.
  "platform": "string"     // "android" | "ios". Required.
}
```

| Field | Type | Required | Validation |
|-------|------|----------|-----------|
| `token` | string | yes | Non-empty; max 4096 chars |
| `platform` | string | yes | Must be `"android"` or `"ios"` |

**Response 200 — registered or updated:**

```json
{
  "id": "string",           // ObjectId of the DeviceTokenEntity
  "userEmail": "string",
  "platform": "string",
  "registeredAt": "string"  // ISO 8601 UTC
}
```

**Error responses:**

| Status | Condition | Body |
|--------|-----------|------|
| 400 | Missing or invalid `token` / `platform` | `{"error": "Invalid request", "detail": "..."}` |
| 401 | Missing or expired JWT | Standard 401 |
| 500 | MongoDB write failure | `{"error": "Internal server error"}` |

**Example request:**

```http
POST /identity/device-token
Authorization: Bearer eyJhbGc...
Content-Type: application/json

{
  "token": "eH4K9...fcmtoken",
  "platform": "android"
}
```

**Example response:**

```json
{
  "id": "64af1c3e2b3f4d5e6a7b8c9d",
  "userEmail": "coach@example.com",
  "platform": "android",
  "registeredAt": "2026-07-31T11:00:00Z"
}
```

---

## Existing Endpoints Used by the Mobile App

### Identity — `POST /identity/login`

Authenticate a provider or customer.

**Authentication:** None (public)

**Request:**

```json
{
  "email": "string",     // Required
  "password": "string"   // Required
}
```

**Response 200:**

```json
{
  "token": "string"    // RS256 signed JWT; claims: sub (email), role, exp
}
```

**Error responses:**

| Status | Condition |
|--------|-----------|
| 400 | Missing email or password |
| 401 | Invalid credentials |

---

### Booking — `GET /booking`

List appointments for the authenticated provider, optionally filtered by date.

**Authentication:** Required — Bearer JWT

**Query parameters:**

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `date` | string (ISO 8601 date) | no | Filter to a specific day (e.g. `2026-07-31`). Omit for all. |

**Response 200:**

```json
[
  {
    "id": "string",
    "emailProvider": "string",
    "emailCustomer": "string",
    "appointmentStatus": "string",   // AppointmentStatus enum value
    "scheduledAt": "string",         // ISO 8601 UTC
    "serviceId": "string"
  }
]
```

**Error responses:** 401 (no/invalid JWT), 403 (provider email mismatch via OwnershipGuard).

---

### Booking — `PUT /booking/{id}`

Update appointment status (confirm, cancel, complete).

**Authentication:** Required — Bearer JWT (provider must own the appointment)

**Path parameter:** `id` — ObjectId of the appointment

**Request:**

```json
{
  "status": "string"   // "Booked" | "Cancelled" | "Completed"
}
```

**Response 200:** Updated appointment object (same shape as GET item above).

**Error responses:**

| Status | Condition |
|--------|-----------|
| 400 | Invalid status transition |
| 401 | No / expired JWT |
| 403 | OwnershipGuard — caller is not the owning provider |
| 404 | Appointment not found |

---

### Calendar — `GET /calendar`

Retrieve 30-day availability for the authenticated provider.

**Authentication:** Required — Bearer JWT

**Query parameters:**

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `from` | string (ISO 8601 date) | no | Start date. Defaults to today. |
| `days` | integer | no | Number of days. Defaults to 30. |

**Response 200:**

```json
[
  {
    "date": "string",          // ISO 8601 date
    "availableSlots": ["string"],  // ISO 8601 time slots
    "bookedSlots": ["string"]
  }
]
```

---

### Customer — `GET /customer`

List customers subscribed to the authenticated provider.

**Authentication:** Required — Bearer JWT (provider)

**Response 200:**

```json
[
  {
    "id": "string",
    "email": "string",
    "fullName": "string"
  }
]
```

---

### Messaging — `GET /messages`

Retrieve the provider's messaging inbox (thread stubs).

**Authentication:** Required — Bearer JWT

**Response 200:**

```json
[
  {
    "threadId": "string",
    "otherPartyEmail": "string",
    "lastMessageBody": "string",
    "lastMessageAt": "string",
    "unreadCount": 0
  }
]
```

---

### Messaging — `GET /messages/thread/{threadId}`

Retrieve all messages in a thread.

**Authentication:** Required — Bearer JWT (caller must be a participant)

**Response 200:**

```json
[
  {
    "id": "string",
    "threadId": "string",
    "senderEmail": "string",
    "body": "string",
    "sentAt": "string",
    "isRead": true
  }
]
```

---

### Messaging — `POST /messages`

Send a message.

**Authentication:** Required — Bearer JWT

**Request:**

```json
{
  "recipientEmail": "string",
  "body": "string"
}
```

**Response 201:** Created `MessageEntity` (same shape as thread item above).

**Error responses:** 400 (empty body), 401, 403 (OwnershipGuard on sender).

---

### Messaging — `PATCH /messages/{id}/read`

Mark a message as read.

**Authentication:** Required — Bearer JWT (recipient only)

**Response 200:** Updated message object.

---

### Notifications — `GET /notifications`

List notifications for the authenticated user.

**Authentication:** Required — Bearer JWT

**Response 200:**

```json
[
  {
    "id": "string",
    "notificationType": "string",   // NotificationType enum value
    "message": "string",
    "createdAt": "string",
    "isRead": false
  }
]
```

---

### Notifications — `PATCH /notifications/{id}/read`

Mark a notification as read.

**Authentication:** Required — Bearer JWT (recipient only)

**Response 200:** Updated notification object.

---

## Pagination

v1 does not paginate any of the list endpoints. The app fetches the full set for the authenticated user. For providers with large volumes (>200 appointments), this is a known limitation to address in v2. The Calendar endpoint's `days` parameter caps that query naturally.

## Rate Limiting

No rate limiting is implemented in v1 (per CONSTITUTION.md — deferred, ADR in DECISIONS.md). The app does not implement client-side throttling; the backend will absorb all requests.

## Error Handling Contract

All HTTP error responses use the shape `{"error": "...", "detail": "..."}` where `detail` is optional. The `JwtDelegatingHandler` intercepts 401 globally. All ViewModels catch `HttpRequestException` and `TaskCanceledException` and surface an error state — no unhandled exceptions (PRD R10, PRD US-007).
