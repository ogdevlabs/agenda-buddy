# Data Model — API Gateway and Mobile Contract (F-015)

**Date:** 2026-08-23 · **Feature ID:** F-015

---

**No data model changes.** This feature operates on existing schema and adds no persisted state.

The gateway is stateless routing — it holds no data of its own, persists nothing, and introduces no new
MongoDB collection, document shape, or field. Every entity this feature's client-side changes touch
(`AppointmentEntity`, `ProviderReport`, notifications, messages, payments) already exists and is unchanged
by this feature; F-015 corrects how the *client* addresses and calls those existing shapes, not the shapes
themselves.

The one client-side "data model" this feature changes is which local storage keys `MobileApp` uses
(`jwt`, `refresh_token` via `ISecureStorageService`) and how they are consumed — the refresh flow and
server-side logout call use the same two keys `AuthService` already reads and writes; no new key is added,
and no existing key changes shape.

`SeedDataProvider`'s fixture data (`Models/` — hand-written providers, customers, and appointments) is
**deleted**, not migrated — it was never real data, and removing it is a code change, not a data migration.
