# Resource group + deploy identity + secrets store for one environment. Aspire's own `azd`
# publisher still owns everything inside this resource group (the Container Apps environment,
# the container registry, and one container app per service/gateway) — see
# AgendaBuddy.AppHost/AppHostWiring.cs and docs/deployment.md. This config exists only for the
# things azd/Aspire has no opinion about and cannot bootstrap non-interactively: the resource
# group itself, the identity CI authenticates as, and where secret values live.

resource "azurerm_resource_group" "environment" {
  name     = "rg-agenda-buddy-${var.environment_name}"
  location = var.location
}

# One Entra app registration per environment, never shared (docs/deployment.md's existing
# "not shared across environments" rule) — a leaked or over-broad credential in one
# environment's federated identity still can't reach another environment's resource group.
resource "azuread_application" "deploy" {
  display_name = "agenda-buddy-deploy-${var.environment_name}"
}

resource "azuread_service_principal" "deploy" {
  client_id = azuread_application.deploy.client_id
}

# GitHub OIDC trust, scoped to this repository and this GitHub Environment specifically — not
# a branch, and not a wildcard — so only a workflow run under this exact Environment name can
# exchange a GitHub token for an Azure one.
resource "azuread_application_federated_identity_credential" "github" {
  application_id = azuread_application.deploy.id
  display_name   = "github-actions-${var.environment_name}"
  description    = "GitHub Actions OIDC for the ${var.environment_name} GitHub Environment"
  audiences      = ["api://AzureADTokenExchange"]
  issuer         = "https://token.actions.githubusercontent.com"
  subject        = "repo:${var.github_repository}:environment:${var.environment_name}"
}

# Contributor to create/update the ACA environment, ACR and container apps that azd's Bicep
# publisher deploys; User Access Administrator because ACA's managed identity needs role
# assignments of its own (e.g. AcrPull) that azd's generated Bicep creates as part of that
# deploy. Both scoped to this environment's resource group only, never the subscription.
resource "azurerm_role_assignment" "contributor" {
  scope                = azurerm_resource_group.environment.id
  role_definition_name = "Contributor"
  principal_id         = azuread_service_principal.deploy.object_id
}

resource "azurerm_role_assignment" "user_access_administrator" {
  scope                = azurerm_resource_group.environment.id
  role_definition_name = "User Access Administrator"
  principal_id         = azuread_service_principal.deploy.object_id
}

# Replaces the AZD_ENV_VARS GitHub secret blob and its `_B64` PEM-encoding workaround: the
# Atlas connection strings, JWT keypair and Kafka endpoint live here instead, readable only by
# this environment's own deploy identity.
resource "azurerm_key_vault" "secrets" {
  name                       = "kv-agbuddy-${var.environment_name}"
  resource_group_name        = azurerm_resource_group.environment.name
  location                   = azurerm_resource_group.environment.location
  tenant_id                  = var.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true

  # Purge protection makes a deleted vault un-purgeable for the soft-delete retention window. Vault
  # names are globally unique, so on a throwaway environment that turns a destroy/re-apply cycle into
  # a name collision that cannot be cleared for weeks. Production environments should set this true.
  purge_protection_enabled = var.key_vault_purge_protection
}

resource "azurerm_role_assignment" "deploy_identity_secrets_officer" {
  scope                = azurerm_key_vault.secrets.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = azuread_service_principal.deploy.object_id
}

# Azure RBAC assignments are eventually consistent — writing a secret immediately after the
# role assignment above can 403 even though `terraform apply` reports it created. A short,
# explicit wait is cheaper than a flaky first apply per environment.
resource "time_sleep" "rbac_propagation" {
  depends_on      = [azurerm_role_assignment.deploy_identity_secrets_officer]
  create_duration = "30s"
}

resource "azurerm_key_vault_secret" "agenda_buddy_connection" {
  name         = "agenda-buddy-connection"
  value        = var.agenda_buddy_connection_string
  key_vault_id = azurerm_key_vault.secrets.id

  depends_on = [time_sleep.rbac_propagation]
}

resource "azurerm_key_vault_secret" "identity_db_connection" {
  name         = "identity-db-connection"
  value        = var.identity_db_connection_string
  key_vault_id = azurerm_key_vault.secrets.id

  depends_on = [time_sleep.rbac_propagation]
}

resource "azurerm_key_vault_secret" "jwt_public_key" {
  name         = "jwt-public-key"
  value        = var.jwt_public_key
  key_vault_id = azurerm_key_vault.secrets.id

  depends_on = [time_sleep.rbac_propagation]
}

resource "azurerm_key_vault_secret" "jwt_private_key" {
  name         = "jwt-private-key"
  value        = var.jwt_private_key
  key_vault_id = azurerm_key_vault.secrets.id

  depends_on = [time_sleep.rbac_propagation]
}
