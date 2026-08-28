output "resource_group_name" {
  value = azurerm_resource_group.state.name
}

output "storage_account_name" {
  value = azurerm_storage_account.state.name
}

output "container_name" {
  value = azurerm_storage_container.state.name
}

# Paste directly into the environment config's `terraform init -backend-config=...` invocation
# (see infra/terraform/environment/backend.tf) plus that environment's own state key.
output "backend_config" {
  description = "resource_group_name / storage_account_name / container_name for -backend-config"
  value = {
    resource_group_name  = azurerm_resource_group.state.name
    storage_account_name = azurerm_storage_account.state.name
    container_name       = azurerm_storage_container.state.name
  }
}
