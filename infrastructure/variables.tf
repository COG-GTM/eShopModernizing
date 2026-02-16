variable "project_name" {
  description = "Name of the project, used as a prefix for all resources"
  type        = string
  default     = "eshop"
}

variable "environment" {
  description = "Deployment environment (dev, staging, prod)"
  type        = string
  default     = "dev"

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be one of: dev, staging, prod."
  }
}

variable "location" {
  description = "Azure region for all resources"
  type        = string
  default     = "eastus"
}

variable "acr_sku" {
  description = "SKU for Azure Container Registry (Basic, Standard, Premium)"
  type        = string
  default     = "Standard"

  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.acr_sku)
    error_message = "ACR SKU must be one of: Basic, Standard, Premium."
  }
}

variable "sql_admin_login" {
  description = "Administrator login for Azure SQL Server"
  type        = string
  sensitive   = true
}

variable "sql_admin_password" {
  description = "Administrator password for Azure SQL Server"
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.sql_admin_password) >= 12
    error_message = "SQL admin password must be at least 12 characters."
  }
}

variable "sql_sku_name" {
  description = "SKU for Azure SQL Database (e.g., S0, S1, GP_S_Gen5_1)"
  type        = string
  default     = "S0"
}

variable "vnet_address_space" {
  description = "Address space for the virtual network"
  type        = list(string)
  default     = ["10.0.0.0/16"]
}

variable "aci_subnet_prefix" {
  description = "Address prefix for the ACI subnet"
  type        = string
  default     = "10.0.1.0/24"
}

variable "sql_subnet_prefix" {
  description = "Address prefix for the SQL private endpoint subnet"
  type        = string
  default     = "10.0.2.0/24"
}

variable "mvc_image_tag" {
  description = "Docker image tag for the MVC application"
  type        = string
  default     = "latest"
}

variable "webforms_image_tag" {
  description = "Docker image tag for the WebForms application"
  type        = string
  default     = "latest"
}

variable "container_cpu" {
  description = "CPU cores allocated to each container instance"
  type        = number
  default     = 1
}

variable "container_memory_gb" {
  description = "Memory (GB) allocated to each container instance"
  type        = number
  default     = 1.5
}

variable "use_mock_data" {
  description = "Whether to use mock data instead of database"
  type        = bool
  default     = false
}

variable "use_azure_storage" {
  description = "Whether to use Azure Blob Storage for images"
  type        = bool
  default     = false
}

variable "azure_storage_connection_string" {
  description = "Connection string for Azure Blob Storage (required if use_azure_storage is true)"
  type        = string
  default     = ""
  sensitive   = true
}

variable "app_insights_instrumentation_key" {
  description = "Application Insights instrumentation key"
  type        = string
  default     = ""
  sensitive   = true
}
