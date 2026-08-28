# Remote state in the storage account infra/terraform/bootstrap creates — never local state
# here, so a second `terraform apply` (whether from CI or a human) always sees the real current
# state instead of silently trying to recreate resources that already exist.
#
# Values are supplied at `terraform init` time, not hardcoded, so this file works unchanged for
# every environment (staging, production, ...) — only the `key` differs, one state blob per
# environment in the same storage account:
#
#   terraform init \
#     -backend-config="resource_group_name=<bootstrap output: resource_group_name>" \
#     -backend-config="storage_account_name=<bootstrap output: storage_account_name>" \
#     -backend-config="container_name=<bootstrap output: container_name>" \
#     -backend-config="key=agenda-buddy-<environment_name>.tfstate"
terraform {
  backend "azurerm" {}
}
