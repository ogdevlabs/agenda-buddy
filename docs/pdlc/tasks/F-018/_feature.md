---
id: F-018
title: refactor-minimal-apis
status: planned
priority: 18
labels: [roadmap, "priority:18"]
claimed_by: oscargarcia@ogdevlabs.onmicrosoft.com
created: 2026-08-18
updated: 2026-08-18
---
Restructure the Minimal API layer following the [Gramli/AuthApi](https://github.com/Gramli/AuthApi) reference — endpoint organisation, validation, and result handling — replacing the current per-service `RequestCollection` hand-wiring that manually constructs CQRS handlers and calls `.Handle()` directly (see `docs/pdlc/context/15-cqrs-and-messaging.md`).

Requested explicitly ahead of F-014–F-017 because a structural refactor of every endpoint is cheaper before those four features add more endpoints to it. Exact scope to be established in Inception.
