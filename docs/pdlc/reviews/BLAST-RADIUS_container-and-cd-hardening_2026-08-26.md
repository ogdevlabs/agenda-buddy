# Blast Radius — container-and-cd-hardening (F-017)

**Scope:** Skipped — docs/config only.

`git diff main..feat/F-017-container-and-cd-hardening --stat -- '*.cs' '*.csproj'` shows only 3 new,
self-contained test files (`DockerAndComposeHygieneTest.cs`, `PinnedThirdPartyActionsTest.cs`,
`PublishContainerTest.cs` — no external callers by construction, they're the leaf consumers) and 4
build-metadata-only `.csproj` edits (removing `CopyToOutputDirectory`/`ErrorOnDuplicatePublishOutputFiles`
items, adding one to `EventsAndCommands.Tests.csproj`). No production method, class, route, or public API
signature was added, renamed, or had its contract changed. The rest of the diff is Dockerfiles (deleted or a
one-line base-image bump), Compose YAML (deleted blocks), CI workflow YAML (new jobs/steps), a Dependabot
config, and a gitleaks rule config — none of it a C#/API symbol a caller could grep for.

Per the blast-radius scope table (`skills/build/steps/blast-radius.md`), this is exactly the "Docs / config
only" case — skip entirely.
