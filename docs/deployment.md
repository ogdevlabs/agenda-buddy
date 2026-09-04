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
- **A Key Vault** holding that environment's secrets (Atlas connection strings, JWT keypair) —
  Terraform writes them in, the deploy workflow reads them out at deploy time

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
| The 7 domain services | processes on dynamic localhost ports | one container app each, internal-only (no external ingress) |
| The Gateway | process on a dynamic localhost port, `MobileApp`'s only address | one container app, **external HTTP ingress** — the only externally reachable resource |
| JWT keys | user secrets, masked in the dashboard | `azd` parameters, stored in Key Vault |
| `WaitFor` gating | yes — mongo health-gates startup | **no** — a connection string has no lifecycle to wait on |

**A dev container is not a production database.** The cloud shape deliberately does not lift the
MongoDB container into the cloud: it exists for a local loop, its persistent volume's lifetime is a
developer's laptop, and it has no backups, failover or an access log worth the name. Cloud MongoDB is
a managed cluster whose connection string is supplied at deploy time, so **nothing in this repository
decides anything about production storage.**

There is no message broker in either shape. Kafka was removed on 2026-09-02 — it had no producers and
no consumers, and an unreachable broker made provider/customer creation fail, so it could only block
signup.

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
- An RSA keypair for JWT signing, **generated for this environment** and not shared with any
  developer's machine

One-time only, per subscription and per new environment (see
[One-time subscription bootstrap](#one-time-subscription-bootstrap)):

- An Azure subscription, and **specifically these permissions** for whoever runs the bootstrap
  `terraform apply` (a subscription Owner has all of them; if you're assembling a narrower
  identity, it needs exactly these three, no more):
  - **Contributor** on the subscription (or at least rights to create resource groups, storage
    accounts, and Key Vaults) — creates the state backend and each environment's resource group
  - **User Access Administrator** on the subscription — creates the RBAC role assignments
    (`Contributor`, `User Access Administrator`, `Key Vault Secrets Officer`) that the new deploy
    identity needs; Contributor alone cannot grant roles to anyone, including itself
  - **Cloud Application Administrator** (or **Application Administrator**) Entra role — creates
    the app registration, service principal, and GitHub OIDC federated credential; this is an
    Entra role, not an Azure RBAC role, and a subscription Owner does **not** automatically have
    it
- .NET 10 SDK and a container runtime locally, only for that first bootstrap run
- A GitHub account with **Settings → Environments** write access on this repository, to create
  the `staging`/`production` Environments and populate their variables/secrets (see
  [Deployments — GitHub Actions](#deployments--github-actions))

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
  -var jwt_public_key="$(cat jwt.pub)" \
  -var jwt_private_key="$(cat jwt.key)"

terraform output client_id            # → GitHub Environment variable AZURE_CLIENT_ID
terraform output resource_group_name  # informational — azd's own output shows this too
```

**2a. Grant yourself data-plane access — before that `apply`, and it is not optional.**

Azure RBAC separates management-plane from data-plane. Subscription **Owner** lets you *create* a Key
Vault and a storage account but grants **no** access to the secrets or blobs inside them, so the apply
above fails 403 writing its own secrets without these two grants:

```bash
MYOID=$(az ad signed-in-user show --query id -o tsv)
az role assignment create --assignee-object-id "$MYOID" --assignee-principal-type User \
  --role "Key Vault Secrets Officer" \
  --scope "/subscriptions/<sub>/resourceGroups/rg-agenda-buddy-<env>/providers/Microsoft.KeyVault/vaults/kv-agbuddy-<env>"
az role assignment create --assignee-object-id "$MYOID" --assignee-principal-type User \
  --role "Storage Blob Data Contributor" \
  --scope "/subscriptions/<sub>/resourceGroups/rg-agenda-buddy-tfstate/providers/Microsoft.Storage/storageAccounts/<state-account>"
```

RBAC is eventually consistent — wait ~60s before running Terraform.

These are deliberately **not** in the Terraform config. They describe who is *operating* the config, not
what the environment should contain, and modelling them as resources made the desired state differ by
caller: with the state file shared between a human's apply and CI's, each run tried to delete the
other's grant. CI did exactly that once, silently removing a developer's Key Vault access.

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
2. **`azd provision` + `azd deploy`**, authenticated as that same identity, reading the Atlas/JWT
   values out of the Key Vault Terraform just confirmed — no `AZD_ENV_VARS` secret blob, no
   `_B64` PEM-encoding workaround. ⚠️ **`provision` defaults to `false`; an environment's first ever
   run must be dispatched with it `true`, or `azd deploy` has no infrastructure to deploy to.**

A **smoke test** closes the loop: after `azd deploy`, the workflow curls the gateway's `/health`
and `/alive` (the only externally-reachable resource — see "Fixed: cloud ingress was backwards
post-Gateway" above) and fails the run if either doesn't return 200, rather than reporting a green
checkmark for a deploy nobody verified came up healthy.

The job declares `environment: ${{ inputs.environment }}`, tying the run to a **GitHub
Environment** matching the Terraform `environment_name` (e.g. `staging`, `production`).

### Secrets and variables go on the GitHub Environment, never the repository

**This is deliberate and non-negotiable for this project.** GitHub resolves an unset
Environment-level value by falling back to the repository-level one of the same name — so setting
anything at **Settings → Secrets and variables** (the repository-wide page) instead of inside a
specific Environment does not fail loudly, it just quietly hands **every** environment the same
Azure identity and the same Atlas/JWT credentials, which defeats the entire reason the
staging/production split exists (one leaked or misused credential would then reach both). Every
value below is set **only** on the Environment page for that one environment — the repository
Settings → Secrets and variables page should have nothing related to this deployment on it at all.

### Step-by-step: configuring one GitHub Environment

Do this once per environment, right after that environment's [bootstrap `terraform
apply`](#one-time-subscription-bootstrap) has run — step 4 reuses the exact values you passed
that `apply` as `-var ...`, and steps 5–6 read its `terraform output`:

1. GitHub → this repository → **Settings** tab → left sidebar **Environments** → **New
   environment**.
2. Type the environment name **exactly** as passed to `terraform apply -var
   environment_name=...` and as you'll type it into the workflow's `environment` input — e.g.
   `staging` (case-sensitive; `Staging` and `staging` are different Environments to GitHub and
   the federated credential's `subject` won't match a mismatched name). Click **Configure
   environment**.
3. For `production` only: under **Deployment protection rules**, enable **Required reviewers**
   and add at least yourself. This is the one deliberately-human gate left — a manual approval
   click even though the workflow is already dispatch-only, for the environment where a mistake
   costs the most. Skip this for `staging`.
4. Under **Environment secrets**, click **Add secret** four times, once per row in the table
   below — name exactly as shown, value is whatever you passed to that environment's bootstrap
   `terraform apply -var ...`:

   | GitHub secret name | Terraform variable it must match | Source |
   |---|---|---|
   | `ATLAS_AGENDA_BUDDY_CONNECTION_STRING` | `agenda_buddy_connection_string` | your Atlas cluster, `agenda_buddy` database |
   | `ATLAS_IDENTITY_DB_CONNECTION_STRING` | `identity_db_connection_string` | your Atlas cluster, `IdentityDb` database |
   | `JWT_PUBLIC_KEY` | `jwt_public_key` | `cat jwt.pub` — the environment's own keypair |
   | `JWT_PRIVATE_KEY` | `jwt_private_key` | `cat jwt.key` — the environment's own keypair |

   These must be the **same values** you passed to the bootstrap `apply`, not new ones — a
   mismatch means the next CI-run `terraform apply` (step 1 of every deploy) reconciles the Key
   Vault to whatever the GitHub secret says, silently overwriting what bootstrap set.
5. Under **Environment variables**, click **Add variable** four times:

   | GitHub variable name | Value |
   |---|---|
   | `AZURE_CLIENT_ID` | output of `terraform output client_id` from this environment's bootstrap apply |
   | `AZURE_TENANT_ID` | your Entra tenant ID |
   | `AZURE_SUBSCRIPTION_ID` | your Azure subscription ID |
   | `AZURE_LOCATION` | the Azure region, e.g. `eastus` (must match `-var location=...` used above) |

6. Also under **Environment variables**, add the three state-backend values from [step 1 of the
   bootstrap](#one-time-subscription-bootstrap) (`terraform output backend_config` in
   `infra/terraform/bootstrap`) — these are the **same three values for every environment** in
   this subscription, since they name the shared state backend, not this environment's own
   resources:

   | GitHub variable name | Terraform bootstrap output |
   |---|---|
   | `TF_STATE_RESOURCE_GROUP` | `resource_group_name` |
   | `TF_STATE_STORAGE_ACCOUNT` | `storage_account_name` |
   | `TF_STATE_CONTAINER` | `container_name` |

7. Repeat this whole procedure for every additional environment (see [Adding another
   environment](#adding-another-environment)) — each gets its own Environment, its own four
   secrets, and its own four `AZURE_*` variables; only the three `TF_STATE_*` variables are
   copied identically across environments.

Once this is done for an environment, running `.github/workflows/deploy.yml` with
`environment: <that name>` needs no further setup — the preflight step re-checks all 13 of the
above by name and fails loudly, naming exactly which is missing, before anything is provisioned
or deployed.

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
3. **The environment's own deploy identity** — step 2 of
   [One-time subscription bootstrap](#one-time-subscription-bootstrap), run once with the new
   `environment_name` (the state backend from step 1 is shared, not recreated).
4. **A matching GitHub Environment** ([above](#deployments--github-actions)), named identically,
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

## Cost control: the dev environment is scheduled off outside working hours

The eight container apps are deployed with `minReplicas: 1`, so they bill continuously whether or not
anyone is using them. Four workflows exist to stop paying for an idle environment:

| Workflow | Trigger | Effect |
|---|---|---|
| `dev-env-schedule.yml` | cron, weekdays | start 09:00, stop 17:00 America/Mexico_City |
| `dev-env-start.yml` | manual | start now |
| `dev-env-stop.yml` | manual | stop now |
| `dev-env-power.yml` | `workflow_call` | the shared logic the other three use |

**Start and stop mean `minReplicas` 1 and 0.** That choice is not arbitrary — the two more obvious
mechanisms do not work:

- `--max-replicas 0` is rejected by the API, which requires `1..1000`.
- `az containerapp revision deactivate` really does stop a revision (`replicas: 0`, `Stopped`), but
  `revision activate` answers **405 Method Not Allowed** while an app is in **Single** revision mode,
  which all eight are. Deactivating is therefore a one-way door, and unusable as half of a start/stop
  pair. Recovering an app deactivated that way means forcing a new revision with
  `az containerapp update --revision-suffix ...`.

`minReplicas` is symmetric, immediate, and leaves everything else untouched — importantly it does not
change ingress, so the gateway keeps the same public FQDN. That matters because
`AgendaBuddy.MobileApp/appsettings.json` has that hostname compiled in.

**Timezone.** GitHub cron is UTC only, and **Mexico City is UTC-6 all year**: Mexico abolished daylight
saving in October 2022, so there is no summer/winter split to handle and one fixed pair of UTC times is
correct in January and July alike. `09:00` local is `15:00Z`; `17:00` local is `23:00Z`. If Mexico ever
reinstates DST, `dev-env-schedule.yml` needs two more crons and nothing else does.

**Weekends are off**, on the reasoning that a dev environment running through Saturday is pure cost. Use
the manual start workflow when that is wrong; it does not conflict with the schedule, because the next
scheduled action is Monday's start and every action is idempotent.

**Stopping is not destroying.** Nothing is deleted: container apps, revisions, images, the Container Apps
environment, the registry, and the Key Vault with its secrets all survive, and starting returns the same
deployment on the same URL. A real teardown is `terraform destroy` plus deleting the resource group, and
is deliberately not behind a button.

**What this does and does not save.** Container Apps consumption is the only scheduled cost. MongoDB
Atlas is an M0 free cluster, so there is nothing to schedule. The Log Analytics workspace bills for
ingestion, which drops when nothing is running but is not switched off. A public HTTPS endpoint attracts
scanners, so a stopped gateway may briefly wake to serve one — seconds of compute, not hours.

## Rollback

`azd` deploys container app revisions, so the fastest rollback is to shift traffic back to the
previous revision in the portal or:

```bash
az containerapp revision list  --name <app> --resource-group <rg> -o table
az containerapp ingress traffic set --name <app> --resource-group <rg> --revision-weight <previous>=100
```

Re-deploying an older commit also works and is slower. Neither restores data — a bad migration is a
database problem, not a deployment one, which is another reason item 5 above matters.
