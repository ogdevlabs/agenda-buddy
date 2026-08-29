# Development Test Accounts

**There is no automatic seed data under the AppHost.** `SeedDevelopmentAccounts`
(`Library/Tools/Migrations/SeedDevelopmentAccounts.cs`) and its sibling `SeedAuthCredentials` both
exist in the codebase but neither is invoked from any service's `Program.cs` — confirmed by
grepping every `Program.cs` in the solution. This matches `docs/pdlc/context/05-data-model.md` and
`12-observability.md`, which already list both as "never invoked". Logging in with
`sarah.mitchell@agendabuddy.dev` / `DevPass123!` (or any of the other accounts this file used to
document) will 401 — that data was never written to Mongo.

`scripts/seed/seed-mongo.sh` and the `compose/data/*.json` fixtures are equally stale for the same
reason: since F-013 (Aspire), services resolve one configured database, not the per-entity
`ProviderDb`/`CustomerDb`/`IdentityDb` names those fixtures target. The README already flags the
script itself as stale (`README.md:224`); this file previously did not carry the same warning for
the Compose fixtures, which was the gap.

## How to actually get test data, right now

Under the AppHost there is exactly one path: register through the real API (or the mobile app's
own "Create Account" screen, which calls the same endpoint). There is no separate "create a
provider/customer profile" step folded into registration — `POST /api/v1/auth/register` only
creates the `CredentialEntity` Identity needs to issue tokens; the Provider/Customer domain record
is a second, separate call. Skipping it means `/api/v1/providers/{email}` or
`/api/v1/customers/{email}` won't resolve even though login succeeds.

### 1. Register (creates the credential, returns tokens)

```bash
GW=http://localhost:<gateway-port>   # from the AppHost console output, or the Aspire dashboard

curl -s -X POST $GW/api/v1/auth/register -H 'Content-Type: application/json' \
  -d '{"email":"you.provider@test.dev","password":"Password123!","role":"Provider"}'
# => {"accessToken":"...", "refreshToken":"..."}
```

`role` must be exactly `"Provider"` or `"Customer"`. Save `accessToken` for the next steps.

### 2. Create the domain profile (a second, separate write)

```bash
TOKEN=<accessToken from step 1>

# Provider:
curl -s -X POST $GW/api/v1/providers -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"firstName":"Pat","lastName":"Provider","email":"you.provider@test.dev","serviceEntities":[],"appointmentEntities":[],"subscribedCustomerCollection":[]}'

# Customer:
curl -s -X POST $GW/api/v1/customers -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"firstName":"Cami","lastName":"Customer","email":"you.customer@test.dev"}'
```

### 3. (Provider only) Add a service, so there's something to book

```bash
curl -s -X PUT $GW/api/v1/services/you.provider@test.dev -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '[{"name":"Consultation","description":"30-min session","fee":50,"feeType":0}]'
```

`feeType` is numeric on the wire (no `JsonStringEnumConverter` is registered): `0` = Hourly,
`1` = Fixed, `2` = Subscription.

### 4. Log in from the mobile app

Use the email/password from step 1 on the app's Sign In screen — no extra setup needed there.

## Suggested identities

Not live accounts — just names to register if you want the same flavor the old seed data had.
Nothing about these strings is special; any email works.

| Role | Suggested email | Suggested specialty |
|------|------------------|----------------------|
| Provider | sarah.mitchell@agendabuddy.dev | Fitness Coach |
| Provider | james.okafor@agendabuddy.dev | Software Instructor |
| Provider | maria.gonzalez@agendabuddy.dev | Therapist / Counselor |
| Customer | alex.chen@agendabuddy.dev | — |
| Customer | priya.sharma@agendabuddy.dev | — |
| Customer | david.thompson@agendabuddy.dev | — |

## An account that already exists locally right now

Unlike the suggested identities above, this one is real — it was registered and given a full
provider profile against this machine's local Mongo volume while validating the mobile-ui-completeness
work (2026-08-28). As long as that volume isn't dropped, it logs in immediately with no setup:

| Field | Value |
|-------|-------|
| Email | `navbar.diag@test.dev` |
| Password | `Password123!` |
| Role | Provider |
| Profile | firstName "Diag", lastName "Provider" |

It also already has `"Accounting"` saved under Professions and a `"Sweep Test Service"` entry under
My Services, both left over from verifying those two features end-to-end — harmless to delete or
ignore.

## Verified end-to-end (2026-08-28)

Steps 1–4 above were run against a live local AppHost as part of validating
`AgendaBuddy.MobileApp`'s full capability set: register (both roles) → create profile → add a
service → browse the provider directory → subscribe → book an appointment → confirm → add a note →
record a payment → cancel a second appointment → deactivate a provider account → password-reset
request. All returned the expected status codes and response shapes. See `agenda-buddy-42q` in the
issue tracker for the full list of what that covered.

## If you want the old auto-seed behavior back

`SeedDevelopmentAccounts` and `SeedAuthCredentials` are real, tested code — they're just not wired
to anything. Making one of them run (e.g., from `AgendaBuddy.AppHost` or a service's `Program.cs`,
gated on `builder.Environment.IsDevelopment()`) is a small, well-scoped follow-up, not a rewrite.
Nobody has picked that up yet.
