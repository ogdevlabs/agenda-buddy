output "client_id" {
  description = "App registration client ID — consumed by deploy.yml's azd federated login step."
  value       = azuread_application.deploy.client_id
}

output "tenant_id" {
  value = var.tenant_id
}

output "subscription_id" {
  value = var.subscription_id
}

output "resource_group_name" {
  value = azurerm_resource_group.environment.name
}

output "key_vault_uri" {
  value = azurerm_key_vault.secrets.vault_uri
}

output "key_vault_name" {
  value = azurerm_key_vault.secrets.name
}
