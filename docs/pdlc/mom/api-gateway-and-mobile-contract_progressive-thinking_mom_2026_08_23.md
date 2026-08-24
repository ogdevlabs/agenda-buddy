# MOM — Progressive Thinking: api-gateway-and-mobile-contract (F-015)

**Date:** 2026-08-23 · **Called by:** Atlas (Product Manager) · **Facilitation:** solo — one model reasoning as
each role, no agents spawned (consistent with F-014/F-016/F-021's sessions; recorded as a fidelity caveat,
not glossed).

---

## Discussion

### Round 1 — Concrete (facts)

All grounded in `docs/pdlc/context/16-mobile-client.md`, `01-api-surface.md`, `09-integrations.md`, and this
brainstorm log's Socratic Discovery:

- No API gateway/reverse proxy exists anywhere in the repo (no YARP, nginx, Envoy, Ingress) — `01-api-surface.md:51`.
- `MauiApp.CreateBuilder()` registers no configuration source; neither `appsettings.json` nor
  `appsettings.Development.json` is ever loaded — both named `HttpClient`s always use the hardcoded fallback
  `http://localhost:6036/` (`MauiProgram.cs:32,38`), which is Identity, over plaintext HTTP.
- Domain route paths are wrong: `GET booking?date=` has no matching backend route at all; `PUT booking/{id}`
  sends `{"status": "Confirmed"}` against an endpoint that now (post-F-014) ignores the status field and
  expects a full `AppointmentEntity`.
- `SeedDataProvider` fires on **both** `HttpRequestException` and a genuine zero-length result
  (`DashboardViewModel.cs:79`, mirrored in `CalendarViewModel`).
- `ErrorMessage` is hardcoded to `string.Empty` and never reassigned in either path, so `HasError` is always
  `false` — the built error banner is structurally unreachable. `IsEmpty` can never be `true` for the same
  reason.
- The refresh token is stored and cleared but never sent to `api/v1/auth/refresh` (grep-confirmed, zero call
  sites). `LogoutAsync` only clears local storage — never calls the server.
- `JwtDelegatingHandler.UnauthorizedAccess` is a `static` event subscribed in `AppShell`'s constructor with
  no unsubscribe.
- `AuthService` checks only `IsSuccessStatusCode`; every error response body (400/409 `{error, message}`
  from Identity) is discarded.
- `MobileApp.csproj` references `Library`, pulling `MongoDB.Driver`, `MongoDB.Bson`, `Stripe.net`, and
  `BCrypt.Net-Next` into the app bundle for a handful of entity shapes.
- The tested TFM (`net10.0` fallback) has **no DI registration and no Shell** — `MauiProgram.cs` and
  `AppShell.xaml.cs` are wrapped in `#if MOBILE`. `RegisterViewModel` has zero test coverage.
- F-014 shipped nine new authenticated routes with contract obligations this client must speak:
  `revenueAvailable`/`revenueUnavailableReason`, empty-notifications-is-normal, non-charging payment
  semantics, and a dedicated `POST .../status` transition route (provider-only completion).
- User decisions already locked in Socratic Discovery: build a real gateway process; remove
  `SeedDataProvider` entirely; fix MobileApp testability in this same feature; wire refresh + server logout;
  replace the `PUT`-based status call with F-014's dedicated route; verify the auth flow live end-to-end.

### Round 2 — Inferential

- **Backend shape:** the gateway is almost certainly a new AppHost resource (an 8th project) declared
  alongside the seven services, wired with `WithReference`/`WaitFor` edges the same way `AppHostWiring.cs`
  already composes MongoDB and Kafka. *(Inference — no gateway code exists yet to confirm against.)*
- **Client shape:** the two named `HttpClient`s and the auth-header-attaching `JwtDelegatingHandler` already
  look correctly split (authenticated vs. no-auth); the fix is likely just pointing both `BaseAddress`es at
  the gateway's single address, not a redesign of the client's HTTP layer.
- **Technology:** YARP is the lowest-friction choice — first-party Microsoft NuGet, .NET-native, no new
  language or runtime, consistent with CONSTITUTION §9's "keep the dependency footprint minimal."
- **Testability fix:** likely means extracting the HTTP-calling logic in each `*ApiService` behind an
  interface constructible under the `net10.0` fallback TFM without the `#if MOBILE`-gated Maui bootstrap —
  the DI registrations for API services are already interface-shaped in places, so this is narrowing an
  existing pattern rather than inventing one.
- **Security surface:** the gateway becomes the natural future home for TLS termination (F-017) and any
  cross-cutting concern that should live in front of all seven services rather than duplicated seven times.
- **Docs:** the OpenAPI specs under `docs/api/openapi/` are already stale after F-014's nine new routes
  (flagged in F-014's own episode as tech debt) — F-015 is the feature that will actually read them, so
  regenerating belongs here.

### Round 3 — Consequential

- **Implementation:** new AppHost resource + gateway project; every `*ApiService`'s route strings corrected
  (`api/v1/` prefix, correct verbs/resource names); the `PUT`-based status call replaced by the dedicated
  `POST .../status` route, which also means the customer-facing UI must hide "mark complete" (provider-only,
  otherwise always refused with 403).
- **Testing:** two new layers of coverage — gateway route-forwarding tests (does `/api/v1/booking/*` actually
  reach Booking through the gateway) and MobileApp wiring tests exercising the corrected paths under a
  testable DI graph, not the `#if MOBILE`-gated one.
- **Security:** the gateway is new attack surface. It must forward the caller's JWT unmodified (not
  re-validate, not strip) — becoming an unauthenticated bypass would be worse than today's status quo, where
  at least each service enforces its own auth.
- **Deployment:** AppHost model tests need a new resource case (mirroring `AppHostWiringTest`'s existing
  pattern); CI needs to build the new gateway project.
- **UX:** the already-built error banner and empty-state UI become reachable "for free" the moment
  `SeedDataProvider` stops intercepting failures and empty results.

### Round 4 — Speculative (risks and unknowns)

- **Phantom:** does the gateway terminate TLS, or does it just proxy over the same plaintext ports? If it's
  a pure proxy, F-015 adds a layer of indirection without touching the actual plaintext-HTTP problem
  (`13-security.md`'s T-103-adjacent finding) — that fix stays F-017's, and the PRD should say so explicitly
  rather than let the gateway *look* like a security improvement it isn't.
- **Echo:** does YARP re-resolve destination addresses per request, or can it cache a stale port across an
  Aspire service restart? Needs a spike before Design commits to YARP's default reverse-proxy config.
- **Muse:** does migrating from the hardcoded fallback to a gateway-relative base URL strand any
  already-cached secure-storage token from an in-place app update? Likely fine (tokens are opaque strings,
  not URL-bound), but worth a explicit check rather than an assumption.
- **Neo:** how does the mobile client itself learn the gateway's address? The backend already solved this
  exact class of problem for Mongo (`MongoConnectionResolver`: Aspire → environment → appsettings, with an
  actionable failure message) — the gateway's address for the client likely needs an equivalent resolution
  story, not a new hardcoded fallback repeating today's defect.
- **Bolt:** F-014's UX-contract items (revenue-unavailable copy, non-charging-payment copy) need actual
  client-facing text — is that a Define-level requirement, or a Design/UX decision? Flagged for Define to
  make explicit rather than leaving it implicit.

### Round 5 — Conflicting

No unresolved conflicts between roles. One tension surfaced and resolved without escalation: Phantom's
concern that the gateway might look like a security fix (Round 4) is resolved by treating TLS termination as
explicitly **out of scope**, owned by F-017 — the PRD will state this so the gateway isn't mistaken for a
completed security control.

### Round 6 — Strategic (design priorities, ranked)

1. **Gateway route-forwarding + auth passthrough, correct and tested** — highest risk; every other fix
   depends on the gateway actually working and not becoming an auth bypass.
2. **Every domain route path/verb/payload shape corrected on the client** — the actual reachability fix this
   feature exists for.
3. **MobileApp wiring made testable under CI** — otherwise this exact defect class can silently return.
4. **`SeedDataProvider` removed; existing error/empty-state UI takes over.**
5. **Refresh-on-401 + server-side logout wired**, verified live end-to-end.
6. **OpenAPI specs regenerated** — lower risk, mechanical, but should land here since F-015 is the first
   consumer of F-014's nine new routes.

---

## Conclusion

1. **Confirmed facts:** see Round 1 — no gateway exists, both config files are dead, the seed-data fallback
   masks two structurally unreachable UX bugs, and the tested TFM has no DI/Shell.
2. **Accepted inferences:** gateway as a new AppHost resource; YARP as the technology; testability fix by
   extracting API-service logic behind constructible interfaces; gateway as F-017's future TLS-termination
   point; OpenAPI regeneration in scope.
3. **Key consequences:** new gateway project + AppHost resource + CI build target; two new test layers; the
   gateway must forward JWTs unmodified; error/empty-state UI becomes reachable for free.
4. **Risks and unknowns:** gateway-as-plaintext-proxy must not be mistaken for a TLS fix (F-017 still owns
   that); YARP's destination-caching behavior against Aspire's dynamic ports needs a spike; the client's own
   discovery of the gateway's address needs a `MongoConnectionResolver`-shaped answer, not a new hardcoded
   fallback; UX-contract copy (revenue-unavailable, non-charging-payment) needs an explicit Define-level
   requirement.
5. **Resolved conflicts:** one (gateway ≠ TLS fix), resolved without user escalation by scoping TLS to F-017.
6. **User escalation answers:** none — this meeting surfaced no question the team could not resolve itself.
7. **Design priorities:** ranked list above.

---

## Escalation

None.
