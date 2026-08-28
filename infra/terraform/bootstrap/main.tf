# One-time, once per Azure subscription — not per environment, and not run on every deploy.
# Creates the storage account that infra/terraform/environment's remote state lives in, so it
# cannot itself use that backend: state for this config stays local, and the resulting
# terraform.tfstate is the one file in this repo's deployment story worth backing up by hand.
# See docs/deployment.md's "One-time subscription bootstrap" section before running this.

terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}

resource "azurerm_resource_group" "state" {
  name     = "rg-agenda-buddy-tfstate"
  location = var.location
}

# Suffixed because storage account names are globally unique across all of Azure, not just
# this subscription.
resource "random_id" "storage_suffix" {
  byte_length = 4
}

resource "azurerm_storage_account" "state" {
  name                     = "tfstateagbuddy${random_id.storage_suffix.hex}"
  resource_group_name      = azurerm_resource_group.state.name
  location                 = azurerm_resource_group.state.location
  account_tier             = "Standard"
  account_replication_type = "ZRS"
  min_tls_version          = "TLS1_2"

  blob_properties {
    versioning_enabled = true
  }
}

# One container, one blob key per environment (agenda-buddy-<name>.tfstate) — set at init time
# in infra/terraform/environment, not a separate container per environment.
resource "azurerm_storage_container" "state" {
  name                  = "tfstate"
  storage_account_id    = azurerm_storage_account.state.id
  container_access_type = "private"
}
