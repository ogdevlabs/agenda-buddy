# 06 — Configuration

**Files:** 17 `appsettings*.json`, 7 `Properties/launchSettings.json`, `docker-compose.yml`, `docker-compose.override.yml`, `global.json`, `Directory.Build.props`, `EventAndCommands/ConfigurationLoader.cs`, `EventAndCommands/LibrarySettings.cs`, the 7 `MongoDbConfiguration.cs` classes.

---

## ⚠️ The single most serious finding: a live database credential is committed

The MongoDB Atlas connection string

```
mongodb+srv://<user>:<REDACTED-ROTATE-THIS>@<cluster>.mongodb.net/...
```

appears verbatim in **14 committed files**:

| File | Line |
|---|---|
| `Booking/appsettings.json` | `:23` |
| `Booking/appsettings.Development.json` | `:3` |
| `Calendar/appsettings.json` | `:23` |
| `Calendar/appsettings.Development.json` | `:3` |
| `Customer/appsettings.json` | `:23` |
| `Customer/appsettings.Development.json` | `:3` |
| `Provider/appsettings.json` | `:23` |
| `Provider/appsettings.Development.json` | `:3` |
| `Services/appsettings.json` | `:23` |
| `Services/appsettings.Development.json` | `:3` |
| `Profession/appsettings.json` | `:23` |
| `Profession/appsettings.Development.json` | `:3` |
| `Identity/appsettings.json` | `:22` |
| `EventAndCommands/appsettings.json` | `:11` |

and once more in `docker-compose.override.yml:114` as the `events` service's `ConnectionStrings` environment variable. Two commented-out blocks (`:88`, `:104`) hold it as well.

This directly violates `CONSTITUTION.md` §4: *"Secrets must never appear in source code — use `appsettings.json` / User Secrets / environment variables."* The credential is in git history, so rotation at the Atlas end is required — removing the files is not sufficient. Cross-reference `13-security.md`.

Note the contrast: the same `docker-compose.override.yml` handles JWT keys correctly, sourcing them from a gitignored `.env` (`:136-138`) with an explicit comment, and the commented-out service blocks carry `# JWT_PUBLIC_KEY must be injected at deploy time — never in source` (`:89`, `:105`). The discipline was applied to JWT keys but not to the database credential.

---

## ⚠️ The two-shape config problem

There are **two different config shapes** for the same MongoDB settings, and the code reads a different one than the primary file declares.

**Shape A — nested under `LibrarySettings` (in `appsettings.json`):**
```json
"LibrarySettings": { "MongoDB": { "ConnectionString": "…", "DatabaseName": "agenda_buddy", … } }
```
`Booking/appsettings.json:21-30` and the same in Calendar, Customer, Provider, Services, Profession, `EventAndCommands`.

**Shape B — at the root (in `appsettings.Development.json`):**
```json
"MongoDB": { "ConnectionString": "…", "DatabaseName": "agenda_buddy", … }
```
`Booking/appsettings.Development.json:2-10` and the same in Calendar, Customer, Provider, Services, Profession.

**What the code reads:**

| Consumer | Anchor | Reads |
|---|---|---|
| `MongoDbConfiguration.MongoClient()` (6 domain services) | e.g. `Booking/Configuration/MongoDbConfiguration.cs:7` | **Shape B** — `GetSection("MongoDB")["ConnectionString"]` |
| `AddMongoDbRepository` (6 domain services) | e.g. `Booking/Extensions/ServiceCollectionExtension.cs:10,14,18,22` | **Shape B** |
| `EventStore` ctor | `EventAndCommands/Persitency/EventStore.cs:9-11` | **Shape B** |
| `ConfigurationLoader.LoadConfiguration()` | `EventAndCommands/ConfigurationLoader.cs:19-26` | **Shape A** — `GetSection("LibrarySettings").GetSection("MongoDB")` |
| Identity | `Identity/Configurations/MongoDbConfiguration.cs:7`, `Identity/Extensions/ServiceCollectionExtension.cs:10,14` | **Shape C** — root `MongoDbSettings`, matching `Identity/appsettings.json:21` |

**Consequences:**

1. ⚠️ **The six domain services only start in `Development`.** Shape B exists solely in `appsettings.Development.json`, which ASP.NET loads only when `ASPNETCORE_ENVIRONMENT=Development`. In `Staging`/`Production`, `GetSection("MongoDB")["ConnectionString"]` is `null` and `new MongoClient(null!)` throws at startup (`MongoDbConfiguration.cs:7`) — before any route is reachable. **Shape A in `appsettings.json` is never read by the services that declare it.**
2. ⚠️ **`ConfigurationLoader` is the only Shape A reader and it is dead code.** Grep confirms it is referenced only by `EventsAndCommands.Tests/ConfigurationLoaderTests.cs`. It also builds its own `ConfigurationBuilder` from `Assembly.GetExecutingAssembly().Location` (`:9-13`) with `AddJsonFile("appsettings.json", optional: false)` — bypassing the host's configuration entirely, and requiring `appsettings.json` to sit next to the DLL (hence the `CopyToOutputDirectory: Always` at `EventAndCommands.csproj:26-28`).
3. ⚠️ **`LibrarySettings`/`MongoDbSettings` (`EventAndCommands/LibrarySettings.cs`) are POCOs bound by nothing.** Only `ConfigurationLoader` constructs them. No `IOptions<T>` pattern is used anywhere in the solution — every consumer reads `IConfiguration` by string key.
4. ⚠️ **Identity's `MongoDbSettings` name collides conceptually with `EventAndCommands.MongoDbSettings`** — same name, different meaning (a config section vs a C# class), and Identity does not use the class.

`Booking/appsettings.Development.json` is also the **only** Development file that omits a `Logging` section, so Booking silently inherits `appsettings.json`'s levels while Identity overrides them (`Identity/appsettings.Development.json:2-8`).

---

## Required configuration keys

### Environment variables (no defaults — fail-fast)

| Variable | Read at | Behaviour if missing |
|---|---|---|
| `JWT_PUBLIC_KEY` | `Library.ServerAuth/AuthenticationExtensions.cs:16` | Throws `ApplicationException` with a clear message (`:19-21`). Required by **all 7 services** |
| `JWT_PRIVATE_KEY` | `Identity/Services/IdentityService.cs:189` | Throws `ApplicationException` (`:190-191`). Required by **Identity only**, and only when a token is minted — so Identity **starts** without it and fails on the first login |
| `ASPNETCORE_ENVIRONMENT` | framework | ⚠️ Must be `Development` or the six domain services will not start (see above) |
| `DOCKER_REGISTRY` | `docker-compose.yml:61,67,73,81` | Defaults to empty via `${DOCKER_REGISTRY-}` |
| `PATH_BASE` | `docker-compose.override.yml:115,139` | ⚠️ **Set but never read** — no `UsePathBase` call exists in any `Program.cs`. Dead |
| `MONGO_HOST` | `scripts/seed/seed-mongo.sh:5` | Defaults to `mongo:27017` |

Both JWT keys are RSA PEM and are passed through `.Replace("\\n", "\n")` (`AuthenticationExtensions.cs:23`, `IdentityService.cs:193`) so they can be supplied as single-line env values.

⚠️ **There is no `.env.example`** in the repo, yet `docker-compose.override.yml:137-138` interpolates `${JWT_PUBLIC_KEY}` / `${JWT_PRIVATE_KEY}` from a `.env` that is gitignored and undocumented. A fresh clone cannot start the Identity container without out-of-band knowledge. `README.md` `[not verified — not read in this scan]`.

### appsettings keys, by service

| Key | Booking | Calendar | Customer | Provider | Services | Profession | Identity | EventAndCommands |
|---|---|---|---|---|---|---|---|---|
| `Logging.LogLevel.Default` | Information | Information | Information | Information | Information | Information | Information | **Debug** |
| `Logging.LogLevel.Provider` | Debug | Debug | — | Debug | — | — | — | — |
| `AllowedHosts` | — | — | `*` | — | `*` | `*` | `*` | — |
| `Kestrel.Endpoints.Http.Url` | :6033 | :6032 | :6034 | :6030 | :6031 | :6035 | :6036 | — |
| `Kestrel.Endpoints.gRPC.Url` | :7033 | :7032 | :7034 | :7030 | :7031 | :7035 | :7036 | — |
| `LibrarySettings.MongoDB.*` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — | ✅ |
| `MongoDbSettings.*` | — | — | — | — | — | — | ✅ | — |
| `…ProfessionsCollection` | — | — | — | — | — | ✅ only | — | — |

⚠️ **`Logging.LogLevel.Provider: Debug`** appears in Booking, Calendar, and Provider (`Booking/appsettings.json:5`) — but `"Provider"` is not a namespace in Booking or Calendar. It is a copy-paste leftover that only means anything in the Provider service. Harmless but misleading.

⚠️ **`EventAndCommands/appsettings.json:4` sets `Default: Debug`** — a class library shipping a `Debug` log level. Since `ConfigurationLoader` is the only reader and it is dead, this has no effect; but the file is copied to output on every build (`EventAndCommands.csproj:26-28`).

⚠️ **`AllowedHosts` is set in 4 of 7 services** and omitted in Booking, Calendar, Provider — inconsistent host filtering.

⚠️ **All Kestrel URLs are `http://localhost:<port>`.** Bound to the loopback interface, so these settings cannot serve traffic from a container or another host. The Dockerfiles expose 8080/8081 (`Booking/Dockerfile:4-5`) — ports that **no `appsettings.json` configures**, so a containerised service binds `localhost:60xx` inside the container and is unreachable through the exposed port. The two configurations contradict each other.

### `launchSettings.json`

Read: `Booking/Properties/launchSettings.json`. Three profiles — `http` (`:12`, `http://localhost:6033`), `https` (`:22`, `https://localhost:8033;http://localhost:6033`), `IIS Express` (`:32`). All set `ASPNETCORE_ENVIRONMENT=Development` and `launchUrl: swagger`.

⚠️ The `https` profile's port **8033** appears nowhere else — not in `appsettings.json`, not in `docker-compose*.yml`, not in the mobile client's `ApiBaseUrl`. **Inference:** `UseHttpsRedirection()` only has a target when launched from this profile. Remaining six `launchSettings.json` files not read; **Inference:** same shape with port offsets.

---

## Docker Compose topology

`docker-compose.yml` declares images/build contexts; `docker-compose.override.yml` supplies ports, env, healthchecks, and the `kafka-net` bridge network (`:158-160`).

| Service | Image | Ports | Notes |
|---|---|---|---|
| `zookeeper` | `confluentinc/cp-zookeeper:7.2.1` | 2181 | |
| `broker` | `confluentinc/cp-server:7.2.1` | 9092, 9101 | Healthcheck via `kafka-topics --list` (`:48-52`) |
| `schema-registry` | `confluentinc/cp-schema-registry:7.2.1` | 8081 | Waits on broker health |
| `kafka0` | `confluentinc/cp-kafka:7.2.1` | — | ⚠️ `command: ["tail", "-f", "/dev/null"]` (`:67`) — **an idle container that does nothing** |
| `kafka-ui` | `provectuslabs/kafka-ui:latest` | 8080 | ⚠️ unpinned `:latest`; `SCHEMAREGISTRY: http://schema-registry:8181` (`:7`) but the registry listens on **8081** (`:62`) — wrong port |
| `kafka-init-topics` | `confluentinc/cp-kafka:7.2.1` | — | Creates `agenda-buddy-topic` and produces `compose/data/message.json` (`:74-77`) |
| `events` | built from `EventAndCommands/Dockerfile` | — | ⚠️ a **class library** run as a service (`08-cicd-deploy.md`) |
| `kafka-library` | built from `Kafka/Dockerfile` | — | ⚠️ also a class library |
| `common-library` | built from `Library/Dockerfile` | — | ⚠️ also a class library |
| `identity` | built from `Identity/Dockerfile` | 6036→80, 7036→81 | The **only API service in compose** |
| `mongo` | `mongo:7` | 27017 | Named volume `mongo-data` (`:149-156`) |

⚠️ **The six domain API services are absent from Compose.** `provider` and `services-api` are commented out (`docker-compose.yml:42-56`, `docker-compose.override.yml:81-109`); Booking, Calendar, Customer, and Profession were never added. `docker compose up` starts Kafka, Mongo, Identity, and three no-op library containers — **you cannot run the application from Compose.**

⚠️ **`identity` maps host 6036 → container 80** (`:141`) and sets `Kestrel__Endpoints__HTTP__Url=http://0.0.0.0:80` (`:130`), correctly overriding the `localhost:6036` from `appsettings.json`. This is the only place the localhost-binding problem is solved — and only for Identity.

⚠️ **`identity` gets `MongoDbSettings__ConnectionString=mongodb://mongo:27017`** (`:133`) pointing at the local container, while `Identity/appsettings.json:22` points at Atlas. So local Compose and local `dotnet run` hit **different databases**.

⚠️ **`events` receives `ConnectionStrings=<atlas-uri>`** (`:114`) — a key nothing reads (the code reads `MongoDB:ConnectionString` / `LibrarySettings:MongoDB:ConnectionString`, never `ConnectionStrings`). Dead env var carrying a live secret.

⚠️ **Kafka uses ZooKeeper** (`cp-*:7.2.1`, `:31`) rather than KRaft — a legacy topology for a 2026 codebase.

⚠️ **`mongo` has no healthcheck and `identity` uses bare `depends_on`** (`:126-127`), so Identity can start before Mongo accepts connections. `IdentityService` catches this as `ServiceUnavailableException` (`:42-45`) and returns 503, so it degrades rather than crashing — but the first requests after `up` will fail.

---

## What is NOT configured here

- **No `IOptions<T>` binding** anywhere — all config access is stringly-typed `GetSection(...)["Key"]`.
- **No configuration validation** — no `ValidateOnStart`, no `ValidateDataAnnotations`. Missing keys surface as `null` reference or `ArgumentNullException` deep in startup.
- **No User Secrets in use** despite three projects declaring a `UserSecretsId` (`EventAndCommands.csproj:8`, `Calendar.csproj:8`, `Profession.csproj:7`) — the other four services have none, and nothing reads secrets.
- **No `.editorconfig`, no StyleCop, no formatter config.** `CONSTITUTION.md` §2 records linter/formatter as "not yet configured"; that is still accurate.
- **No `appsettings.Production.json`** or any non-Development environment file for any service.
- **No connection pooling / timeout / retry settings** on the Mongo client — `new MongoClient(connectionString)` with driver defaults only.
- **No feature flags, no `IConfiguration` reload handling** (`reloadOnChange: true` is set in `ConfigurationLoader.cs:13` for the dead path only).
