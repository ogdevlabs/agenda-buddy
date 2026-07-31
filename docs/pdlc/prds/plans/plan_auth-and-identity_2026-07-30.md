# Plan: Auth and Identity

**Feature:** auth-and-identity
**Date:** 2026-07-30
**PRD:** [PRD_auth-and-identity_2026-07-30.md](../PRD_auth-and-identity_2026-07-30.md)

---

## Tasks

| Beads ID | Title | Labels | Depends On | Author | Created (UTC) |
|----------|-------|--------|------------|--------|---------------|
| agenda-buddy-if7 | CredentialEntity + credentials collection indexes | backend, story:US-001/US-002 | — | ogdevlabs | 2026-07-31T04:59:24Z |
| agenda-buddy-yo1 | AddAgendaBuddyAuthentication() Library extension + startup fail-fast | backend, story:US-006/US-009 | — | ogdevlabs | 2026-07-31T04:59:24Z |
| agenda-buddy-gm8 | Identity microservice scaffold + MongoDB config | backend, devops, story:US-001 | if7, yo1 | ogdevlabs | 2026-07-31T04:59:24Z |
| agenda-buddy-1yc | IdentityService: register + login endpoints | backend, story:US-001/US-002/US-003 | gm8 | ogdevlabs | 2026-07-31T04:59:24Z |
| agenda-buddy-t34 | IdentityService: refresh + logout endpoints | backend, story:US-004/US-005 | gm8 | ogdevlabs | 2026-07-31T04:59:24Z |
| agenda-buddy-9n0 | Wire auth middleware into all six consumer service Program.cs files | backend, devops, story:US-006 | yo1 | ogdevlabs | 2026-07-31T04:59:24Z |
| agenda-buddy-7h2 | OwnershipGuard helper + handler-level ownership checks | backend, story:US-007/US-008 | yo1 | ogdevlabs | 2026-07-31T04:59:24Z |
| agenda-buddy-146 | Pre-auth credentials migration script | backend, story:US-010 | if7 | ogdevlabs | 2026-07-31T04:59:24Z |
| agenda-buddy-bd8 | Identity.Tests: unit + integration test suite (auth harness) | backend, story:US-001/US-003/US-004/US-005 | 1yc, t34 | ogdevlabs | 2026-07-31T04:59:24Z |
| agenda-buddy-e4b | Auth middleware matrix tests + IDOR tests (401/403 full matrix) | backend, story:US-006/US-007/US-008 | 9n0, 7h2, bd8 | ogdevlabs | 2026-07-31T04:59:24Z |

---

## Dependency Graph

```
agenda-buddy-e4b (Auth matrix + IDOR tests)
    ├── agenda-buddy-bd8 (Identity.Tests harness)
    │   ├── agenda-buddy-t34 (refresh + logout)
    │   │   └── agenda-buddy-gm8 (Identity scaffold)
    │   │       ├── agenda-buddy-yo1 (Library auth extension)  ← Wave 1
    │   │       └── agenda-buddy-if7 (CredentialEntity)        ← Wave 1
    │   └── agenda-buddy-1yc (register + login)
    │       └── agenda-buddy-gm8 (Identity scaffold)           ← Wave 2
    ├── agenda-buddy-9n0 (six-service wiring)
    │   └── agenda-buddy-yo1 (Library auth extension)          ← Wave 1
    └── agenda-buddy-7h2 (OwnershipGuard + ownership checks)
        └── agenda-buddy-yo1 (Library auth extension)          ← Wave 1

agenda-buddy-146 (migration script)
    └── agenda-buddy-if7 (CredentialEntity)                    ← Wave 1
```

---

## Implementation Order

**Wave 1 — Foundation (parallel)**
- `agenda-buddy-if7`: CredentialEntity + indexes
- `agenda-buddy-yo1`: AddAgendaBuddyAuthentication() Library extension

**Wave 2 — Identity scaffold (sequential after Wave 1)**
- `agenda-buddy-gm8`: Identity microservice project, MongoDB config, Docker wiring

**Wave 3 — Parallel build (all unblock after Wave 2 / Wave 1)**
- `agenda-buddy-1yc`: register + login endpoints (unblocks after gm8)
- `agenda-buddy-t34`: refresh + logout endpoints (unblocks after gm8)
- `agenda-buddy-9n0`: six-service auth wiring (unblocks after yo1)
- `agenda-buddy-7h2`: OwnershipGuard + handler checks (unblocks after yo1)
- `agenda-buddy-146`: migration script (unblocks after if7)

**Wave 4 — Test suite (sequential after Wave 3 endpoints)**
- `agenda-buddy-bd8`: Identity.Tests harness (unblocks after 1yc + t34)

**Wave 5 — Full matrix (sequential after all Wave 3 + Wave 4)**
- `agenda-buddy-e4b`: Auth middleware matrix + IDOR tests (unblocks after 9n0, 7h2, bd8)
