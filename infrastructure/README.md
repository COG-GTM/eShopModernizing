# eShop Modernizing - Azure Cloud Infrastructure

This directory contains Terraform infrastructure-as-code (IaC) for deploying the containerized eShop applications to Azure.

## Architecture Overview

```
                    +------------------+
                    |   GitHub Actions  |
                    |   CI/CD Pipeline  |
                    +--------+---------+
                             |
                    Build & Push Images
                             |
                             v
+-----------------------------------------------------------+
|                    Azure Resource Group                     |
|                                                             |
|  +-------------------+     +-----------------------------+ |
|  | Azure Container   |     |        Virtual Network       | |
|  | Registry (ACR)    |     |                               | |
|  |                   |     |  +----------+ +-----------+  | |
|  | - modernizedmvc   |---->|  | ACI      | | SQL       |  | |
|  | - modernizedweb   |     |  | Subnet   | | Subnet    |  | |
|  |   forms           |     |  |          | |           |  | |
|  +-------------------+     |  | +------+ | | +-------+ |  | |
|                            |  | | MVC  | | | | Azure | |  | |
|                            |  | | App  | | | | SQL   | |  | |
|                            |  | +------+ | | | DB    | |  | |
|                            |  |          | | +-------+ |  | |
|                            |  | +------+ | |           |  | |
|                            |  | | Web  | | | Private   |  | |
|                            |  | | Forms| | | Endpoint  |  | |
|                            |  | +------+ | |           |  | |
|                            |  +----------+ +-----------+  | |
|                            |                               | |
|                            +-----------------------------+ | |
|                                                             |
|  NSG Rules:                                                 |
|  - ACI: Allow HTTP/HTTPS inbound                           |
|  - SQL: Allow 1433 from ACI subnet only                    |
+-----------------------------------------------------------+
```

### Components

| Resource | Purpose |
|----------|---------|
| **Azure Container Registry** | Stores Docker images for MVC and WebForms applications |
| **Azure Container Instances** | Runs the containerized applications (Windows containers for .NET Framework 4.7.2) |
| **Azure SQL Database** | Replaces the local SQL Server container with a managed database service |
| **Virtual Network** | Network isolation with dedicated subnets for ACI and SQL |
| **Private Endpoint** | Secures SQL Database traffic within the VNet |
| **Network Security Groups** | Controls inbound/outbound traffic per subnet |

## Prerequisites

- [Terraform](https://developer.hashicorp.com/terraform/downloads) >= 1.5.0
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) >= 2.50
- An Azure subscription with Owner or Contributor access
- A storage account for Terraform remote state (see [Remote State Setup](#remote-state-setup))

## Quick Start

### 1. Remote State Setup

Create the Azure Storage account for Terraform state:

```bash
az group create --name tfstate-rg --location eastus

az storage account create \
  --name <your-storage-account> \
  --resource-group tfstate-rg \
  --sku Standard_LRS \
  --encryption-services blob

az storage container create \
  --name tfstate \
  --account-name <your-storage-account>
```

Update the `backend` block in `main.tf` with your storage account name.

### 2. Configure Variables

```bash
cp terraform.tfvars.example terraform.tfvars
```

Edit `terraform.tfvars` with your values. At minimum, set:
- `sql_admin_login` and `sql_admin_password`
- `location` (Azure region)
- `environment` (dev, staging, or prod)

### 3. Deploy

```bash
az login

terraform init
terraform plan -out=tfplan
terraform apply tfplan
```

### 4. Verify Deployment

After deployment, Terraform outputs the application URLs:

```bash
terraform output mvc_app_url
terraform output webforms_app_url
```

## CI/CD Pipeline

The GitHub Actions workflow (`.github/workflows/deploy.yml`) automates the full build-and-deploy cycle:

| Stage | Description |
|-------|-------------|
| **Build** | Restores NuGet packages and builds the .NET solutions |
| **Test** | Runs unit tests for both MVC and WebForms projects |
| **Docker** | Builds Docker images and pushes to ACR |
| **Deploy** | Applies Terraform to provision/update Azure infrastructure |

### Required GitHub Secrets

| Secret | Description |
|--------|-------------|
| `AZURE_CREDENTIALS` | Service principal credentials JSON for Azure login |
| `ACR_LOGIN_SERVER` | ACR login server URL (e.g., `myacr.azurecr.io`) |
| `ACR_USERNAME` | ACR admin username |
| `ACR_PASSWORD` | ACR admin password |
| `SQL_ADMIN_LOGIN` | Azure SQL administrator login |
| `SQL_ADMIN_PASSWORD` | Azure SQL administrator password |
| `TFSTATE_STORAGE_ACCOUNT` | Storage account name for Terraform state |
| `TFSTATE_RESOURCE_GROUP` | Resource group containing the state storage account |
| `ARM_CLIENT_ID` | Azure service principal client ID |
| `ARM_CLIENT_SECRET` | Azure service principal client secret |
| `ARM_SUBSCRIPTION_ID` | Azure subscription ID |
| `ARM_TENANT_ID` | Azure AD tenant ID |

### Environment Separation

The pipeline supports three environments configured via the `environment` variable:

- **dev** — Triggered on pushes to feature branches
- **staging** — Triggered on pushes to `main`
- **prod** — Manual trigger only (workflow_dispatch) with approval gates

## Terraform Files

| File | Description |
|------|-------------|
| `main.tf` | Provider configuration, remote state backend, resource group |
| `variables.tf` | Input variable definitions with validation rules |
| `outputs.tf` | Output values for deployed resource URLs and connection info |
| `acr.tf` | Azure Container Registry resource |
| `aci.tf` | Azure Container Instances for MVC and WebForms apps |
| `sql.tf` | Azure SQL Server, database, and private endpoint |
| `networking.tf` | Virtual network, subnets, and network security groups |
| `terraform.tfvars.example` | Sample variable values for quick setup |

## Resource Naming Convention

All resources follow the pattern: `{type}-{project}-{environment}-{random_suffix}`

Examples:
- Resource Group: `rg-eshop-dev-abc123`
- ACR: `acreshopdevabc123`
- SQL Server: `sql-eshop-dev-abc123`
- VNet: `vnet-eshop-dev-abc123`

## Destroying Resources

To tear down all Azure resources:

```bash
terraform destroy
```

> **Warning**: This will permanently delete all resources including the SQL database. Ensure you have backups before proceeding.
