# Development Seed Accounts

Pre-configured test accounts for local development. These are seeded automatically when the `SeedDevelopmentAccounts` migration runs in Development mode.

## Credentials

All accounts use the same password: **`DevPass123!`**

## Providers

| Name | Email | Specialty |
|------|-------|-----------|
| Sarah Mitchell | sarah.mitchell@agendabuddy.dev | Fitness Coach |
| James Okafor | james.okafor@agendabuddy.dev | Software Instructor |
| Maria Gonzalez | maria.gonzalez@agendabuddy.dev | Therapist / Counselor |

### Sarah Mitchell — Fitness Coach

| Service | Fee | Fee Type |
|---------|-----|----------|
| Personal Training Session | $75.00 | Hourly |
| Group Fitness Class | $25.00 | Fixed |
| Monthly Coaching Plan | $250.00 | Subscription |

### James Okafor — Software Instructor

| Service | Fee | Fee Type |
|---------|-----|----------|
| Python Tutoring | $60.00 | Hourly |
| Full-Stack Bootcamp Prep | $500.00 | Fixed |
| Weekly Code Review | $200.00 | Subscription |

### Maria Gonzalez — Therapist / Counselor

| Service | Fee | Fee Type |
|---------|-----|----------|
| Individual Therapy Session | $120.00 | Hourly |
| Couples Counseling | $150.00 | Hourly |
| Mindfulness Workshop | $45.00 | Fixed |

## Customers

| Name | Email |
|------|-------|
| Alex Chen | alex.chen@agendabuddy.dev |
| Priya Sharma | priya.sharma@agendabuddy.dev |
| David Thompson | david.thompson@agendabuddy.dev |

## How to Seed

### Option 1: C# Migration (Startup Seeder)

The `SeedDevelopmentAccounts` migration runs at application startup when `ASPNETCORE_ENVIRONMENT=Development`. It:

1. Inserts provider entities with embedded services into the Provider collection
2. Inserts customer entities into the Customer collection
3. Creates `CredentialEntity` records in the Identity database with bcrypt-hashed passwords
4. Skips any accounts that already exist (idempotent)

To invoke manually:

```csharp
await SeedDevelopmentAccounts.RunAsync(providerRepo, customerRepo, credentialRepo, logger);
```

### Option 2: Docker Compose Fixtures

JSON seed files are in `compose/data/`:

- `seed-providers.json` — 3 providers with services
- `seed-customers.json` — 3 customers

These can be imported with `mongoimport`:

```bash
mongoimport --uri="<connection-string>" --db=ProviderDb --collection=providers --jsonArray --file=compose/data/seed-providers.json
mongoimport --uri="<connection-string>" --db=CustomerDb --collection=customers --jsonArray --file=compose/data/seed-customers.json
```

## Testing Flows

With these accounts you can test:

1. **Provider Registration** — Register via `/api/v1/auth/register` with role `"Provider"`
2. **Customer Registration** — Register via `/api/v1/auth/register` with role `"Customer"`
3. **Login** — POST to `/api/v1/auth/login` with any seeded email + `DevPass123!`
4. **Service Browsing** — Providers already have services attached
5. **Booking Flow** — Customers can book appointments with providers
6. **Mobile App** — Use any account email + password in the app login screen

## Notes

- Email domain `@agendabuddy.dev` is used to distinguish seed accounts from real data
- Seed accounts have `MustResetPassword = false` (unlike migration-created credentials)
- The migration is idempotent — running it multiple times will not create duplicates
- These accounts should NEVER be deployed to production
