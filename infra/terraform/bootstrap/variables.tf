variable "subscription_id" {
  description = "Azure subscription that holds the Terraform state backend."
  type        = string
}

variable "location" {
  description = "Azure region for the state backend's resource group and storage account."
  type        = string
  default     = "eastus"
}
