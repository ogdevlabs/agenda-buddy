# ISSUE-001 — AppHost never launches the 7 services

**Status:** OPEN · **Severity:** P1 — must be fixed · **Filed:** 2026-08-17
**Feature:** F-013 aspire-wiring · **Branch:** `feat/F-013-aspire-wiring`
**Tracker:** `agenda-buddy-6sl` (beads) · **Bisection log:** `docs/pdlc/tasks/F-013/F-013-T14.md`
**Blocks:** AC-1.1, AC-1.2, AC-1.3, AC-2.3, AC-3.4 — i.e. the entire headline purpose of F-013

---

## Symptom

`dotnet run --project AgendaBuddy.AppHost` brings up MongoDB, Kafka and the Aspire dashboard, but **none of the seven API services ever start.**

DCP creates only the dashboard executable and **0** `executablereplicasets` for the projects, while still registering their 20 DCP `services` and 6 `endpoints`. **Nothing is logged** — no error, no `FailedToStart`. The graph is built; the launch never happens.

```bash
KC=$(ls -d /var/folders/*/*/T/aspire-dcp*/ | tail -1)kubeconfig
kubectl --kubeconfig "$KC" get executables
# NAME                        CREATED AT
# aspire-dashboard-sxppvqsc   ...        ← only this; the 7 services are absent
```

The rest of F-013 is complete and otherwise merge-ready: 286 tests passing, 0 warnings, no outstanding Critical review findings, and all 7 services start correctly **outside** the AppHost in both `Development` and `Staging`.

## Blocker 1 — proven primary cause

**The generic `AddProject<TProject>` overload creates no DCP executable.** Demonstrated side-by-side, both in the same graph in the same run:

| How the project was added | DCP executable created? |
|---|---|
| `AddProject<Projects.Booking>("booking", launchProfileName: null)` | ❌ no |
| `AddProject("booking-via-path", "../Booking/Booking.csproj")` | ✅ **launched** |

The generic overload resolves SDK-generated metadata at `obj/Debug/net10.0/Aspire/references/*.ProjectMetadata.g.cs`, whose `SuppressBuild` is `true`. The `ProjectPath` it contains is correct and the `.csproj` exists — verified. A minimal throwaway AppHost launched its project fine, so **Aspire project launching works on this machine**; the fault is in our wiring, not the environment.

## Blocker 2 — real, not yet narrowed

With the path overload, all 7 services launched in **exactly** this configuration:

```csharp
builder.AddProject(name, projectPath).WithReference(database)   // 7/7 launched
```

Re-adding these three **together** returns to 0 executables:

1. the `foreach (… EndpointAnnotation) { Port = null; TargetPort = null; }` loop — how AC-1.4 avoids the hardcoded `localhost:603x` ports
2. `.WaitFor(mongo)` / `.WaitFor(kafka)` — edge case E-6
3. `.WithEnvironment("JWT_PUBLIC_KEY", jwtPublicKey)` where the value is a **secret `ParameterResource`** — threat T-003

A single-line parameter value fails identically, so the multi-line PEM is **not** the cause.

## Ruled out

- **Untrusted dev certificate** — `dotnet dev-certs https --trust` was run; no change.
- **Container runtime** — Rancher Desktop works; both containers come up healthy (`mongo:8.3`, `confluentinc/confluent-local:8.2.0`).
- **Missing build output / wrong `ProjectPath`** — both verified present and correct.

> ⚠️ **Do not trust the "ruled out" list in attempt 1 of `F-013-T14`.** Those experiments each removed one feature *while still using the broken generic overload*, so they demonstrated nothing. Blocker 2 has genuinely not been narrowed to a single cause.

## Suggested resolution path (~15 minutes)

**Step 0.** Switch the seven `AddApi` calls from `AddProject<Projects.X>(name, launchProfileName: null)` to `AddProject(name, relativeCsprojPath)`. **Keep** the `ProjectReference` items in `AgendaBuddy.AppHost.csproj` so the services still build with the AppHost and the AC-1.5 no-MobileApp guard stays enforceable.

**Step 1.** Confirm the baseline launches 7/7 with only `.WithReference(database)`.

**Step 2.** Add back **one** feature at a time (~3 min per run), checking DCP after each with the `kubectl get executables` command above:

| Order | Add back | If it blocks |
|---|---|---|
| a | `WaitFor(mongo)` | Aspire's Mongo health check isn't reaching `Healthy`. Investigate that check rather than dropping `WaitFor` — E-6 exists because a service that starts before Mongo accepts connections fails its first request. |
| b | port clearing | AC-1.4 needs a different mechanism than post-hoc annotation mutation. Try `WithEndpoint(name, callback)`, or remove the Kestrel-derived annotations entirely instead of nulling their ports. |
| c | parameter-backed `WithEnvironment` | Fallback is a plain string via `builder.Configuration["Parameters:jwt-public-key"]`. **This loses dashboard masking and weakens the threat T-003 mitigation** — surface the tradeoff explicitly, do not take it silently. |

**Step 3.** Re-run the five unverified criteria and update `docs/pdlc/design/aspire-wiring/verification.md`, plus review finding A-3 (observe that JWT masking survives into the dashboard).

## Environment notes

- Rancher Desktop puts `docker` at `~/.rd/bin`, which is **not on PATH** by default, and Aspire shells out to `docker`: `export PATH="$HOME/.rd/bin:$PATH"` first.
- The Rancher VM here is **2 CPUs / 4.1 GB** and already runs a k8s cluster. Mongo + Kafka + 7 services is tight; consider raising the allocation.
- JWT keys for the AppHost live in user secrets: `dotnet user-secrets set "Parameters:jwt-public-key" "<pem>" --project AgendaBuddy.AppHost`.

## Definition of done

- [ ] `dotnet run --project AgendaBuddy.AppHost` starts all 7 services
- [ ] Dashboard lists 9 resources, all 7 services `Healthy` (AC-1.2, AC-1.3)
- [ ] AC-1.4 still holds — `AppHostWiringTest.NoServiceBindsAHardcodedHostPort` still passes and no service binds `localhost:603x`
- [ ] `WaitFor` ordering retained, or its removal consciously accepted and recorded
- [ ] JWT keys still masked in the dashboard, or the T-003 tradeoff recorded in `DECISIONS.md`
- [ ] `verification.md` updated; F-013-T14 closed
