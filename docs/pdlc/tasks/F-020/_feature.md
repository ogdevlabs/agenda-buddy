---
id: F-020
title: api-refactor-rollout
status: in_progress
priority: 21
labels: [roadmap, "priority:21"]
depends_on: [F-019]
claimed_by: oscargarcia@ogdevlabs.onmicrosoft.com
created: 2026-08-18
updated: 2026-08-27
---
**Stage 3 of 3 in the API refactor program (F-018 → F-019 → F-020).** Roll the shape proven on `Booking` in F-019 across the remaining six services: `Calendar`, `Customer`, `Provider`, `Services`, `Profession`, `Identity`.

Ends the two-styles-in-one-codebase state that F-019 deliberately creates. Deletes the six remaining `RequestCollection` classes and the six duplicated exception-handler blocks, and removes the last persistence entities from route signatures.

Scope is deliberately deferred: F-019's outcome decides how much of the pattern generalises, and whether the shared abstractions want extracting into a common project. Do not plan this before F-019 ships.
