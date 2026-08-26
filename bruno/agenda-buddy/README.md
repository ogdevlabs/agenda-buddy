# Agenda Buddy — Bruno collection

A [Bruno](https://www.usebruno.com/) API collection covering all 7 backend services, routed through the
Gateway (`api/v1/{service}/**`) the same way `MobileApp` reaches them — plus one folder (`7-Health`) that
talks to each service directly, since `/health`/`/alive` are deliberately outside the Gateway's allowlist
(T-302).

This file is for browsing the repo. Bruno's own in-app "Docs" tab on the collection root (`collection.bru`)
has the same content and is what you'll actually see while working — open the collection in Bruno and click
the collection name to read it there.

## Steps to get started

1. **Start the backend.** From the repo root: `dotnet run --project AgendaBuddy.AppHost` (starts MongoDB,
   Kafka, all 7 services, and the Gateway — 8 processes). See the repo's `CLAUDE.md` for first-run secrets
   setup if this is a new machine.
2. **Open this collection in Bruno** (`bruno/agenda-buddy/`) and pick an environment — top-right dropdown:
   - **Local (Aspire AppHost)** — matches step 1. Aspire assigns every port dynamically, so fill in
     `gatewayUrl` yourself each run (the Aspire dashboard lists it) — and the seven `*Url` variables too,
     but only if you need `7-Health`.
   - **Local (standalone)** — for running one service at a time with `dotnet run --project <Service>
     --no-launch-profile`, using each service's fixed `appsettings.json` port. `gatewayUrl` still needs
     filling in by hand even here — the Gateway has no fixed port of its own, only ever running under the
     AppHost.
3. **Run `0-Auth/2 Login`** (register first with `0-Auth/1 Register` if you don't have an account). This
   captures `accessToken`/`refreshToken` into the environment automatically. Nearly every route requires
   this JWT — without it you get `401` on everything except the two `Profession` reads.
4. **Register a Provider before a Customer**, if you need both roles: `2-Customer/List customers` and
   `1-Provider/Create provider` both require the `Provider` role, and a `Customer`-role token gets `403` on
   them by design.
5. **Run any request.** `Authorization: Bearer {{accessToken}}` is sent automatically on every request via
   the collection-level auth setting.

## JSON, not HTML, on error responses

Every service's error pipeline (`AddProblemDetails` + `UseExceptionHandler` + `UseStatusCodePages`) returns
`application/problem+json` by default — this is server-side behavior, not something Bruno controls. The one
narrow exception is each service's **Development-only** unhandled-exception fallback (only reachable when a
service runs with `ASPNETCORE_ENVIRONMENT=Development`, which the AppHost does not use — see
`*/Extensions/HttpContextExtensions.cs`'s `AcceptsJson()`): if a request's `Accept` header doesn't indicate
JSON, that one fallback path replies `text/plain` instead of JSON. The collection now sends
`Accept: application/json` at the collection level (alongside the existing `Content-Type: application/json`)
so every request always asks for JSON back, closing that gap.

If you're still seeing an HTML response somewhere, it's most likely Swagger UI (`/swagger`) rather than an
API route — Swagger is only available when a service runs standalone as `Development`; it's not exposed
under the AppHost at all (see the note in `collection.bru`'s docs).

## Regenerating the OpenAPI specs

```bash
./scripts/generate-openapi.sh              # all seven
./scripts/generate-openapi.sh Provider     # just one
```

Writes `docs/api/openapi/<Service>.json` plus a browsable `index.md`. Safe to run any time — it uses a
throwaway keypair and a disposable Mongo container, and touches no real data.
