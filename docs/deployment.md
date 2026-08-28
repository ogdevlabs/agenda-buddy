# Deploying Agenda Buddy to the cloud

**Status: capability added, not yet exercised.** The wiring, the `azd` project file and the deploy
workflow are in place and unit-tested, but **no deployment has been performed from this repository
yet**. The first one must be run by hand from a workstation (see [First deployment](#first-deployment-run-this-by-hand)) — the GitHub Actions
workflow is for the second onwards. Nothing below should be read as attested until someone has run
it; where a step could not be verified locally, that is stated.

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

- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) (`azd`)
- .NET 10 SDK, and a container runtime — `azd` builds images locally before pushing
- An Azure subscription, and permission to create a resource group, container apps environment,
  container registry and Log Analytics workspace
- **A managed MongoDB** reachable from Azure, with its own credential — *not* the credential in
  `docs/issues/ISSUE-002-atlas-credential-rotation.md`, which is compromised
- **A managed Kafka** (Confluent Cloud, or Event Hubs' Kafka endpoint) — or drop the three Kafka
  consumers from the deployment until one exists
- An RSA keypair for JWT signing, **generated for this environment** and not shared with any
  developer's machine

## First deployment — run this by hand

`azd` is interactive on first use, and that is a feature: it discovers the parameters from the graph
and asks for each one. Do this once, from a workstation, and note down what it asks for.

```bash
azd auth login
azd init          # only if .azure/ does not exist yet; azure.yaml is already committed
azd env new staging --location <region> --subscription <subscription-id>

# Values azd will prompt for, or set them up front:
azd env set <name-azd-uses-for-agenda-buddy> "mongodb+srv://<user>:<password>@<cluster>/agenda_buddy?retryWrites=true&w=majority"
azd env set <name-azd-uses-for-IdentityDb>   "mongodb+srv://<user>:<password>@<cluster>/IdentityDb?retryWrites=true&w=majority"
azd env set <name-azd-uses-for-kafka>        "<bootstrap-host>:9092"
azd env set <name-azd-uses-for-jwt-public-key>  "$(cat jwt.pub)"
azd env set <name-azd-uses-for-jwt-private-key> "$(cat jwt.key)"

azd up            # provision + deploy
azd show          # the ingress URL of each service
```

> The exact environment-variable names `azd` derives from the resource graph are **not documented
> here on purpose** — they are a function of the azd and Aspire versions, and writing a guess into
> this file would be worse than writing nothing. `azd up` prints them; capture them then.

Verify the deployment the same way the local run is verified:

```bash
curl -sS https://<service-ingress>/health   # expect 200 Healthy — this runs the MongoDB check
curl -sS https://<service-ingress>/alive    # expect 200 Healthy
```

`/health` exercising MongoDB is what proves the connection string reached the container correctly. If
it returns `503`, the service is running and its database is not — check the connection string and
the Atlas network access list before touching anything else.

## Subsequent deployments — GitHub Actions

`.github/workflows/deploy.yml`, **manual dispatch only**. It authenticates with federated
credentials (OIDC), so there is no long-lived Azure secret in this repository.

One-time setup:

1. Create an Entra app registration with a **federated credential** for this repository, and give it
   Contributor plus User Access Administrator on the target resource group.
2. Repository **variables**: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`,
   `AZURE_LOCATION`.
3. Repository **secret** `AZD_ENV_VARS`: a newline-separated `KEY=VALUE` list of the parameter values
   from the first deployment. A `KEY` ending in `_B64` is base64-decoded before use — that is how the
   multi-line PEM keys travel, since a `KEY=VALUE` line cannot carry newlines:

   ```
   AGENDA_BUDDY_CONNECTION=mongodb+srv://...
   JWT_PUBLIC_KEY_B64=LS0tLS1CRUdJTiBQVUJMSUMg...
   ```

4. Run the workflow with `provision: true` the first time infrastructure changes, `false` for a
   code-only deploy.

**Unverified:** this workflow has never run. It could not be exercised from the development machine —
there is no Azure subscription wired up here, and inventing a successful run would be worthless. Its
preflight step is written to fail loudly and name every missing value rather than deploy something
half-configured, which is the failure mode the `CI_JWT_*` guard in `dotnet.yml` demonstrated.

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
3. **No staging/production separation.** One `azd` environment and one Atlas cluster. A deploy is a
   deploy.
4. **The dashboard is a privileged surface.** The Aspire dashboard exposes environment variables,
   configuration, logs and traces for every resource. Do not expose it publicly in a deployed
   environment.
5. **No database backups or restore drill.** Atlas can do both; nobody has configured or tested them.
6. **No smoke test after deploy.** The workflow deploys and reports endpoints; it does not verify
   `/health` came back green. Add that before anyone relies on a green checkmark.
7. **Secrets have no rotation story.** The JWT keys are set once by hand. Rotating them means
   restarting all seven services with a new pair, which is currently an undocumented manual dance.

## Rollback

`azd` deploys container app revisions, so the fastest rollback is to shift traffic back to the
previous revision in the portal or:

```bash
az containerapp revision list  --name <app> --resource-group <rg> -o table
az containerapp ingress traffic set --name <app> --resource-group <rg> --revision-weight <previous>=100
```

Re-deploying an older commit also works and is slower. Neither restores data — a bad migration is a
database problem, not a deployment one, which is another reason item 5 above matters.
