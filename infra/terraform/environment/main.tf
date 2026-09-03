# Resource group + deploy identity + secrets store for one environment. Aspire's own `azd`
# publisher still owns everything inside this resource group (the Container Apps environment,
# the container registry, and one container app per service/gateway) — see
# AgendaBuddy.AppHost/AppHostWiring.cs and docs/deployment.md. This config exists only for the
# things azd/Aspire has no opinion about and cannot bootstrap non-interactively: the resource
# group itself, the identity CI authenticates as, and where secret values live.

resource "azurerm_resource_group" "environment" {
  name     = "rg-agenda-buddy-${var.environment_name}"
  location = var.location

  # azd's generated template declares this same group (targetScope = 'subscription') and stamps an
  # azd-env-name tag on it. Without this, the two tools fight: Terraform strips the tag on every apply
  # and azd restores it on every provision.
  lifecycle {
    ignore_changes = [tags]
  }
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
# SUBSCRIPTION scope, not resource-group scope, and this is forced by azd rather than chosen.
#
# Aspire's publisher generates `targetScope = 'subscription'` and declares the resource group as a
# resource it creates itself (verified with `azd infra synth`). AZURE_RESOURCE_GROUP does not override
# it. So a resource-group-scoped Contributor cannot even pass deployment validation -- the first real
# provision failed with AuthorizationFailed on
# Microsoft.Resources/deployments/validate/action over /subscriptions/<id>.
#
# The consequence is real and worth stating plainly: this identity, triggerable from GitHub, holds
# standing write access to the whole subscription. That is acceptable only because this subscription
# holds nothing but this application's non-production environments. Do NOT reuse this pattern on a
# subscription that also holds production -- give production its own subscription instead.
#
# User Access Administrator is needed on top of Contributor because the generated Bicep creates role
# assignments of its own (the container apps' managed identity needs AcrPull).
resource "azurerm_role_assignment" "contributor" {
  scope                = "/subscriptions/${var.subscription_id}"
  role_definition_name = "Contributor"
  principal_id         = azuread_service_principal.deploy.object_id
}

resource "azurerm_role_assignment" "user_access_administrator" {
  scope                = "/subscriptions/${var.subscription_id}"
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

# Terraform's own state. The deploy identity's Contributor grant is scoped to this environment's
# resource group only (least privilege, and deliberately so), but the state backend is a shared
# storage account in a DIFFERENT resource group -- so `terraform init` failed 403 in CI before it
# could read state at all, while succeeding locally purely because a human runs it as Owner.
#
# Granted a blob DATA role rather than key access, and the backend authenticates via AAD
# (see backend.tf), so no storage account key is ever issued to anyone.
locals {
  # Constructed, not looked up. A data source would need Microsoft.Storage/storageAccounts/read, which
  # is management-plane and which the deploy identity deliberately does not have on this shared
  # resource group -- the first CI apply failed 403 on exactly that read. The ID format is stable.
  state_storage_account_id = join("", [
    "/subscriptions/", var.subscription_id,
    "/resourceGroups/", var.state_resource_group_name,
    "/providers/Microsoft.Storage/storageAccounts/", var.state_storage_account_name,
  ])
}

# Reader on the state resource group, so the deploy identity can READ the two role assignments below
# that this config declares. Terraform refreshes what it manages on every run, and without this the
# refresh itself 403s even when nothing has changed. Reader grants no write and no data access -- the
# blob access comes from the data-plane role, and state contents are still unreadable without it.
resource "azurerm_role_assignment" "deploy_identity_state_reader" {
  scope = join("", [
    "/subscriptions/", var.subscription_id,
    "/resourceGroups/", var.state_resource_group_name,
  ])
  role_definition_name = "Reader"
  principal_id         = azuread_service_principal.deploy.object_id
}

resource "azurerm_role_assignment" "deploy_identity_state_blob" {
  scope                = local.state_storage_account_id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azuread_service_principal.deploy.object_id
}

# Azure RBAC assignments are eventually consistent — writing a secret immediately after the
# role assignment above can 403 even though `terraform apply` reports it created. A short,
# explicit wait is cheaper than a flaky first apply per environment.
resource "time_sleep" "rbac_propagation" {
  depends_on      = [azurerm_role_assignment.deploy_identity_secrets_officer]
  create_duration = "60s"
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

# Created only when a key was supplied. Key Vault rejects an empty secret value, so storing the
# variable's own "not configured" default would fail the apply -- and an absent secret is the more
# honest representation of "this environment has no mail provider" anyway. The deploy workflow treats
# it as optional when reading.
resource "azurerm_key_vault_secret" "resend_api_key" {
  count = var.resend_api_key == "" ? 0 : 1

  name         = "resend-api-key"
  value        = var.resend_api_key
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
