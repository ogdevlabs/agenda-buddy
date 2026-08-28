# Deploying Agenda Buddy to the cloud

**Status: capability added, not yet exercised.** The wiring, the `azd` project file, the
Terraform bootstrap/environment configs and the deploy workflow are in place and validated
(`terraform validate`, `actionlint`, and the AppHost's own unit tests), but **no deployment has
been performed from this repository yet**. There is no manual `azd`/`az` command anywhere in
this deployment's steady-state path — the one-time exceptions are named explicitly in
[One-time subscription bootstrap](#one-time-subscription-bootstrap) below, and both of them are
about creating the identity the automation later authenticates as, which cannot bootstrap
itself from nothing. Nothing below should be read as attested until someone has run it; where a
step could not be verified locally, that is stated.

---

## Why Azure Container Apps

Aspire's deployment model is: the AppHost's resource graph *is* the deployment description. A
publisher walks that graph and emits infrastructure. Azure Container Apps is the target Aspire
supports first-class, via the Azure Developer CLI (`azd`), and it is the right fit here for reasons
specific to this app:

- **Eight small HTTP processes** (seven domain services + the Gateway) map one-to-one onto container
  apps, with scale-to-zero. Nothing here justifies managing Kubernetes.
- **The AppHost already knows the topology** — who talks to whom, who waits on what, which service
  gets which connection string. AKS would mean re-describing all of it in YAML.
- **Aspire's telemetry lands somewhere useful for free**: `AddServiceDefaults` exports OTLP, and the
  ACA environment ships a Log Analytics workspace.

The alternatives were considered and rejected for this stage: **AKS** (real cost, real operator
burden, no benefit at eight small processes), **App Service** (no first-class Aspire publisher, and
no scale-to-zero for containers), and **Aspir8 / plain Kubernetes manifests** (adds a toolchain
without removing one).

## Why Terraform, and for exactly what

Terraform is **not** a supported Aspire publisher — the AppHost's resource graph is, and `azd` is
what turns it into the Container Apps environment, the registry and the container apps
themselves. Using Terraform for that same layer would mean fighting Aspire's own model, which is
the opposite of "follow Aspire's deployment guidelines." So Terraform (`infra/terraform/`) is
scoped to exactly what `azd`/Aspire has no opinion about and cannot bootstrap non-interactively:

- **The resource group** each environment's resources live in
- **The GitHub Actions deploy identity** — an Entra app registration with a federated (OIDC)
  credential scoped to one GitHub Environment, plus the role assignments `azd` needs to operate
  inside that resource group
- **A Key Vault** holding that environment's secrets (Atlas connection strings, JWT keypair,
  Kafka endpoint) — Terraform writes them in, the deploy workflow reads them out at deploy time

Everything inside the resource group that Aspire's own publisher already knows how to build —
the Container Apps environment, the registry, one container app per service — stays owned by
`azd`, unchanged from the design above. See `infra/terraform/{bootstrap,environment}/` and ADR-058
in `docs/pdlc/memory/DECISIONS.md`.

## What deploys, and what does not

The graph is built in one of two shapes, chosen by `AppHostWiring.DeploymentTarget` — defaulting to
`Cloud` when the AppHost is publishing and `Local` when it is running:

| | Local (`dotnet run`) | Cloud (`azd up`) |
|---|---|---|
| MongoDB | container + persistent volume, password from user secrets | **connection string parameter** — managed cluster (Atlas) |
| Kafka | container, no volume (E-10) | **connection string parameter** — managed Kafka |
| The 7 domain services | processes on dynamic localhost ports | one container app each, internal-only (no external ingress) |
| The Gateway | process on a dynamic localhost port, `MobileApp`'s only address | one container app, **external HTTP ingress** — the only externally reachable resource |
| JWT keys | user secrets, masked in the dashboard | `azd` parameters, stored in Key Vault |
| `WaitFor` gating | yes — mongo and kafka health-gate startup | **no** — a connection string has no lifecycle to wait on |

**A dev container is not a production database.** The cloud shape deliberately does not lift the
MongoDB or Kafka containers into the cloud: they exist for a local loop, one has a persistent volume
whose lifetime is a developer's laptop, and neither has backups, failover or an access log worth the
name. Cloud MongoDB is a managed cluster whose connection string is supplied at deploy time, so
**nothing in this repository decides anything about production storage.**

Both shapes are asserted by `AgendaBuddy.AppHost.Tests`: the cloud shape provisions no data
containers, supplies each data service as a connection string under the same resource name as
locally, waits for nothing, binds no hardcoded port, and keeps the JWT keys secret.

### Fixed: cloud ingress was backwards post-Gateway

`AppHostWiring.cs`'s `AddApi` helper used to unconditionally call `.WithExternalHttpEndpoints()` on
each of the seven domain services in the `Cloud` shape — a comment there read *"the mobile app calls
every service directly, so each one needs ingress."* That was true before the Gateway (F-015)
shipped, and stopped being fixed once `MobileApp` moved to calling only the Gateway. Found and fixed
before any real deployment (the code had never been exercised — ADR-035): `.WithExternalHttpEndpoints()`
now lives only on the `gateway` resource; the seven domain services stay internal-only. A second,
related gap surfaced while fixing this: the Gateway resource had **zero** `EndpointAnnotation`s at
all (Aspire derives them from a project's `appsettings.json` `Kestrel:Endpoints` block, and Gateway
has none), so marking "external" had nothing to mark — `gateway` now gets an explicit
`.WithHttpEndpoint(name: "http")` so it has a real endpoint to publish. Both are asserted by
`AppHostWiringTest.CloudTargetExposesOnlyTheGatewayExternally`, so a regression fails a test rather
than only surfacing on the first real `azd up`.

## Prerequisites

Ongoing (every deploy, all automated in CI — nothing here is run by hand):

- [Terraform](https://developer.hashicorp.com/terraform/install) and
  [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
  (`azd`) — only needed locally if you're changing `infra/terraform/` itself; the deploy workflow
  installs both for you
- **A managed MongoDB** reachable from Azure, with its own credential — *not* the credential in
  `docs/issues/ISSUE-002-atlas-credential-rotation.md`, which is compromised
- **A managed Kafka** (Confluent Cloud, or Event Hubs' Kafka endpoint) — or drop the three Kafka
  consumers from the deployment until one exists
- An RSA keypair for JWT signing, **generated for this environment** and not shared with any
  developer's machine

One-time only, per subscription and per new environment (see
[One-time subscription bootstrap](#one-time-subscription-bootstrap)):

- An Azure subscription, and permission to create resource groups, Entra app registrations,
  role assignments, and Key Vaults — needed once, by whoever runs the bootstrap `terraform apply`
- .NET 10 SDK and a container runtime locally, only for that first bootstrap run

## One-time subscription bootstrap

Two `terraform apply` runs, each genuinely one-time, and both about creating the identity the
*rest* of this document's automation later authenticates as — an identity can't create the trust
relationship that lets it authenticate itself, so exactly these two steps are run by a human with
their own `az login`, and nowhere else in this deployment story is.

**1. State backend — once per Azure subscription, never again after that:**

```bash
cd infra/terraform/bootstrap
az login
terraform init
terraform apply -var subscription_id=<subscription-id> -var location=<region>
terraform output backend_config    # capture these three values — the next step needs them
```

**2. This environment's own deploy identity — once per new environment** (`staging`,
`production`, ...; repeated for each one, see
[Adding another environment](#adding-another-environment)):

```bash
cd infra/terraform/environment
az login
terraform init \
  -backend-config="resource_group_name=<from step 1: resource_group_name>" \
  -backend-config="storage_account_name=<from step 1: storage_account_name>" \
  -backend-config="container_name=<from step 1: container_name>" \
  -backend-config="key=agenda-buddy-<environment-name>.tfstate"

terraform apply \
  -var environment_name=<environment-name> \
  -var location=<region> \
  -var subscription_id=<subscription-id> \
  -var tenant_id=<tenant-id> \
  -var agenda_buddy_connection_string="mongodb+srv://<user>:<password>@<cluster>/agenda_buddy?retryWrites=true&w=majority" \
  -var identity_db_connection_string="mongodb+srv://<user>:<password>@<cluster>/IdentityDb?retryWrites=true&w=majority" \
  -var kafka_bootstrap_servers="<bootstrap-host>:9092" \
  -var jwt_public_key="$(cat jwt.pub)" \
  -var jwt_private_key="$(cat jwt.key)"

terraform output client_id            # → GitHub Environment variable AZURE_CLIENT_ID
terraform output resource_group_name  # informational — azd's own output shows this too
```

That second `apply` creates the Entra app registration, its GitHub OIDC federated credential
(scoped to this exact GitHub Environment name), the role assignments `azd` needs inside the new
resource group, and a Key Vault holding the five secret values passed above. From here on,
**every deploy of this environment — including its first — runs unattended from GitHub Actions**
(see below); nobody runs `azd auth login`, `azd env set`, or any other interactive command again
for this environment.

## Deployments — GitHub Actions

`.github/workflows/deploy.yml`, **manual dispatch only** (see the comment at the top of that file
for why push-triggering waits on the rest of the "Before this is production" list). It has two
stages, both non-interactive:

1. **`terraform apply`** against `infra/terraform/environment`, authenticated via the same GitHub
   OIDC federated credential the bootstrap step created — reconciles the resource group, identity
   and Key Vault against any drift, and outputs this environment's `client_id`/`key_vault_name`.
2. **`azd provision` + `azd deploy`**, authenticated as that same identity, reading the Atlas/JWT/
   Kafka values out of the Key Vault Terraform just confirmed — no `AZD_ENV_VARS` secret blob, no
   `_B64` PEM-encoding workaround.

A **smoke test** closes the loop: after `azd deploy`, the workflow curls the gateway's `/health`
and `/alive` (the only externally-reachable resource — see "Fixed: cloud ingress was backwards
post-Gateway" above) and fails the run if either doesn't return 200, rather than reporting a green
checkmark for a deploy nobody verified came up healthy.

The job declares `environment: ${{ inputs.environment }}`, tying the run to a **GitHub
Environment** matching the Terraform `environment_name` (e.g. `staging`, `production`) — this is
what keeps environments isolated, so configuration below is set **per GitHub Environment**, never
at the repository level (a repo-level value would hand every environment the same Azure identity
and the same secrets, defeating the point of the split).

Per-environment GitHub configuration (set once, when [bootstrapping that environment](#one-time-subscription-bootstrap), not
per deploy):

- **Settings → Environments** → an Environment named identically to the `azd`/Terraform
  environment name (case-sensitive).
- **Environment variables**: `AZURE_CLIENT_ID` (from `terraform output client_id`),
  `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_LOCATION`, and the three
  `TF_STATE_RESOURCE_GROUP` / `TF_STATE_STORAGE_ACCOUNT` / `TF_STATE_CONTAINER` values from step 1
  of the bootstrap (shared across every environment in the same subscription — they name the
  *state backend*, not this environment's own resources).
- **Environment secrets**: `ATLAS_AGENDA_BUDDY_CONNECTION_STRING`, `ATLAS_IDENTITY_DB_CONNECTION_STRING`,
  `KAFKA_BOOTSTRAP_SERVERS`, `JWT_PUBLIC_KEY`, `JWT_PRIVATE_KEY` — the same five values passed to
  the bootstrap `terraform apply` above, so a later `terraform apply` run from CI reconciles to
  the same state rather than drifting.
- For `production` specifically, a **required reviewers** protection rule — a manual approval
  click even though the workflow is already dispatch-only, the one deliberately-human gate for
  the environment where a mistake costs the most.

**Unverified:** this workflow has never run. It could not be exercised from the development
machine — there is no Azure subscription wired up here, and inventing a successful run would be
worthless. Its preflight step fails loudly and names every missing value rather than deploying
something half-configured, the failure mode the `CI_JWT_*` guard in `dotnet.yml` demonstrated.

## Adding another environment

Nothing in `infra/terraform/`, `azure.yaml`, or the AppHost's resource graph hardcodes a single
environment name — every resource is parameterized by it, and Terraform state is keyed by it, so
two environments never collide as long as the names differ. Replicating this setup to a new
environment (e.g. going from just `staging` to also having `production`) means repeating, in full,
for the new name:

1. **A dedicated MongoDB Atlas cluster and credential.** Never share a cluster (or the compromised
   one named in `docs/issues/ISSUE-002-atlas-credential-rotation.md`) across environments — a bug or
   a leaked credential in one environment must not reach another's data.
2. **A dedicated RSA keypair for JWT signing**, generated fresh for that environment — never the
   same keypair as another environment or a developer's machine.
3. **A dedicated managed Kafka target**, or a deliberate decision to drop the three Kafka consumers
   for that environment.
4. **The environment's own deploy identity** — step 2 of
   [One-time subscription bootstrap](#one-time-subscription-bootstrap), run once with the new
   `environment_name` (the state backend from step 1 is shared, not recreated).
5. **A matching GitHub Environment** ([above](#deployments--github-actions)), named identically,
   with its own scoped variables and secrets — so `deploy.yml`'s
   `environment: ${{ inputs.environment }}` resolves to the right, isolated identity for that run.

None of this requires a code or workflow change — the replication is entirely a matter of
repeating the above with a new name and genuinely distinct credentials per environment, not
shared ones.

## Before this is production

This gets a working staging deployment. It is **not** a production posture, and the gaps are
specific:

1. **⚠️ Rotate the Atlas credential first, or deploy against a fresh cluster.** See
   `docs/issues/ISSUE-002-atlas-credential-rotation.md`. Deploying to the cloud while a credential
   with full read/write access to the cluster sits in public git history means the deployment and the
   attacker share a database.
2. **Ingress topology is now correct** (only the Gateway is externally reachable), but the Gateway
   alone has no rate limiting, no WAF, and no single place to revoke a token — front it with Azure
   Front Door or API Management before real users exist.
3. **No staging/production separation exists yet.** The mechanism to create one is documented above
   ([Adding another environment](#adding-another-environment)) and requires no code change, but as
   of this writing only one environment has actually been provisioned. A deploy today is still a
   deploy against whatever single environment exists until a second one is created following that
   checklist.
4. **The dashboard is a privileged surface.** The Aspire dashboard exposes environment variables,
   configuration, logs and traces for every resource. Do not expose it publicly in a deployed
   environment.
5. **No database backups or restore drill.** Atlas can do both; nobody has configured or tested them.
6. **Secrets rotation is a `terraform apply`, but nothing triggers one.** Rotating the JWT keypair
   or an Atlas credential means re-running `terraform apply` on `infra/terraform/environment` with
   new variable values (updates the Key Vault) followed by a deploy (so the running containers
   pick up the new values) — the *mechanism* exists, but nothing schedules or reminds anyone to do
   it, and each service still restarts with the new pair rather than rotating without downtime.

## Rollback

`azd` deploys container app revisions, so the fastest rollback is to shift traffic back to the
previous revision in the portal or:

```bash
az containerapp revision list  --name <app> --resource-group <rg> -o table
az containerapp ingress traffic set --name <app> --resource-group <rg> --revision-weight <previous>=100
```

Re-deploying an older commit also works and is slower. Neither restores data — a bad migration is a
database problem, not a deployment one, which is another reason item 5 above matters.
