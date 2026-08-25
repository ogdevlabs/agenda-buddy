# Data Model: container-and-cd-hardening (F-017)
<!-- pdlc-template-version: design-doc -->

No data model changes. This feature operates entirely on build configuration, CI workflow definitions, and repository structure (Dockerfiles, `docker-compose*.yml`, `.github/workflows/dotnet.yml`, `.github/dependabot.yml`). It reads, writes, and deletes no MongoDB collection, adds no entity, and touches no `[BsonElement]` mapping. `EventAndCommands.csproj`'s `appsettings.json` metadata change (Requirement 3) affects which file lands where at publish time — it is a build artifact, not persisted or runtime application data.
