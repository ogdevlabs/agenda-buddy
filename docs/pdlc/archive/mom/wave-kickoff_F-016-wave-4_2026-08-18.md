# MOM — Wave 4 Kickoff Standup

**Feature:** `secure-public-endpoints` (F-016)
**Date:** 2026-08-18T20:22:00Z
**Called by:** Neo (Architect)
**Participants:** Neo, Bolt, Pulse, Echo — 4 agents
**Spawn mode:** **solo** — standing session instruction not to spawn agents overrides STATE's
`Party Mode: agent-teams`. Same as every prior F-016 meeting; fidelity is lower, weigh accordingly.

---

## Wave under discussion

| Task | Title | Labels | Depends on | ACs |
|---|---|---|---|---|
| `F-016-T05` | `TokenFactory` — valid / expired / foreign-subject / no-`sub` RS256 | `backend`, `security` | T03 ✅ | AC-6 |
| `F-016-T06` | `ServiceHostFixture` — real HTTP + Mongo Testcontainer, fail-closed guard | `backend`, `security`, `devops` | T03 ✅, T04 ✅ | AC-4, AC-5, **AC-20 `[security]` T-002 CRITICAL** |

`T06` is the plan's second bottleneck and carries the feature's only CRITICAL security AC. Both
feed `T07`.

---

## Round 1 — findings

### Bolt (Backend Engineer)

**Genuinely parallel, for a specific reason: T06's AC-4 needs no token.** AC-18 requires
`GET /api/v1/professions` to remain anonymous, so T06 can satisfy AC-4 — "starts a service, issues a
real HTTP request to a real route, asserts the response" — against Profession's anonymous read route
with no `Authorization` header at all. Reaching for an authenticated route instead would give T06 a
hidden dependency on T05 and collapse the wave to sequential.

**Token shape T05 must mirror exactly** (`Identity/Services/IdentityService.cs:200-213`):

| Claim | Value | Note |
|---|---|---|
| `JwtRegisteredClaimNames.Sub` | the email | Maps **inbound** to `ClaimTypes.NameIdentifier`, which is what `OwnershipGuard` reads (`Tools/OwnershipGuard.cs:9,16`) |
| `ClaimTypes.Role` | `Provider` / `Customer` | Round-trips via the JWT handler's outbound/inbound claim-type maps, so `IsInRole` works |
| `JwtRegisteredClaimNames.Jti` | a GUID | |
| issuer | `agenda-buddy-identity` | `ValidateIssuer = true` with this exact value (`AuthenticationExtensions.cs:38-39`) |
| algorithm | `RS256` | `ValidAlgorithms = ["RS256"]` — the only accepted one |
| audience | *(none)* | `ValidateAudience = false` |

### Pulse (DevOps)

**`JWT_PUBLIC_KEY` must be in the environment before the host builds — even for an anonymous route.**
`AuthenticationExtensions.cs:16-21` throws `ApplicationException` at DI-registration time when it is
absent or blank. So `ServiceHostFixture` must join `HarnessCollection` and consume
`CryptoSessionFixture.PublicKeyPem`. This confirms wave 3's design rather than changing it.

Note also `AuthenticationExtensions.cs:23` does `publicKeyPem.Replace("\\n", "\n")` — harmless for a
real newline-bearing PEM, but it means a PEM passed with literal `\n` escapes also works. No action.

### Echo (QA Engineer) — the findings that matter

**🔍 E-1 — the guard must not blindly overwrite `ConnectionStrings__mongodb`.** A fixture that
unconditionally sets the variable to its own container makes the guard vacuous: it would be comparing
its own value to itself. `MongoConnectionResolver` (`Library/Configuration/MongoConnectionResolver.cs:13-19`)
reads **four** keys in priority order —

```
ConnectionStrings:mongodb          (Aspire, primary)
MongoDbSettings:ConnectionString   (Identity's shape)
MongoDB:ConnectionString           (legacy Development)
LibrarySettings:MongoDB:ConnectionString
```

— so overwriting one proves nothing about the other three. The guard must inspect the **resolved**
value, through the same resolver the service uses, and abort when it is not the container's.

**🔍 E-2 — compare container identity, not hostname shape.** Already in the task body, restated
because it is the single most likely thing to be "simplified" later: assert equality against
`container.GetConnectionString()` — the endpoint the Testcontainers API reports for the container
*this fixture started*. A `localhost`/`127.0.0.1` pattern check is explicitly insufficient; Pulse
broke that version at the threat party, because `kubectl port-forward` and an SSH tunnel to Atlas both
present as localhost, and a developer may legitimately run Mongo locally.

**🔍 E-3 — "no database or collection is created" needs a positive observation.** It is a negative
claim, so asserting it by *not* seeing something is unfalsifiable. The workable form: after the abort,
connect to the container and assert the expected test database does not appear in its database list.

### Neo — newly measured, and it narrows the work

Every `MongoDB:ConnectionString` and `MongoDbSettings:ConnectionString` across all seven services'
`appsettings.json` and `appsettings.Development.json` is the **empty string** — F-013's credential
removal blanked them rather than deleting the keys — and `Resolve` skips empty values
(`MongoConnectionResolver.cs:45`).

So **no appsettings path can leak today.** The guard should still check the resolved value rather than
one variable, because the resolver is the contract and appsettings can be refilled by anyone. But the
live hazard is exactly one thing: a stray environment variable in the developer's shell. Worth knowing
before someone spends the task hardening four paths that are all currently inert.

---

## Round 2 — cross-talk

Not required. Bolt's parallelism argument and Echo's guard findings concern different tasks and do not
interact.

---

## Wave Execution Plan

### Confirmed safe parallel

Both. `T05` writes `Harness/TokenFactory.cs`; `T06` writes `Harness/ServiceHostFixture.cs`. No shared
file. T06's independence rests on Bolt's anonymous-route finding.

### Flagged sequential pairs

**None.**

### Recommended ordering

1. **`F-016-T05`** — small and fully specified; landing it first means `T07` unblocks the instant T06 does.
2. **`F-016-T06`** — the bottleneck, with the CRITICAL AC. Carries E-1, E-2 and E-3.

### Dependency updates applied

**None.** Verified against `tasks.cjs dep tree`.

---

## Carried into the tasks

| ID | Finding | Owner |
|---|---|---|
| B-1 | Satisfy AC-4 against the anonymous `GET /api/v1/professions` route, so T06 stays independent of T05 | T06 |
| B-2 | Mirror `IdentityService`'s claim set and issuer exactly, or tokens validate inconsistently with production | T05 |
| P-1 | `JWT_PUBLIC_KEY` must be set before the host builds — `AuthenticationExtensions` throws at DI time | T06 |
| E-1 | Never blindly overwrite the connection string; check the **resolved** value across all four keys | T06 |
| E-2 | Assert container **identity** via `GetConnectionString()`, never a `localhost` pattern | T06 |
| E-3 | Prove "no database created" by inspecting the container's database list after the abort | T06 |
| N-1 | All four appsettings paths are currently empty strings — the live hazard is one env var | T06 |
