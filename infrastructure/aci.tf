resource "azurerm_container_group" "mvc" {
  name                = "aci-mvc-${local.name_suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Windows"
  ip_address_type     = "Public"
  dns_name_label      = "eshop-mvc-${local.name_suffix}"
  restart_policy      = "Always"

  image_registry_credential {
    server   = azurerm_container_registry.main.login_server
    username = azurerm_container_registry.main.admin_username
    password = azurerm_container_registry.main.admin_password
  }

  container {
    name   = "eshop-mvc"
    image  = "${azurerm_container_registry.main.login_server}/eshop/modernizedmvc:${var.mvc_image_tag}"
    cpu    = var.container_cpu
    memory = var.container_memory_gb

    ports {
      port     = 80
      protocol = "TCP"
    }

    environment_variables = {
      "UseMockData"             = tostring(var.use_mock_data)
      "UseCustomizationData"    = "False"
      "UseAzureStorage"         = tostring(var.use_azure_storage)
      "UseAzureActiveDirectory" = "false"
    }

    secure_environment_variables = {
      "CatalogDBContext"              = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.catalog.name};User Id=${var.sql_admin_login};Password=${var.sql_admin_password};Encrypt=true;TrustServerCertificate=false;"
      "StorageConnectionString"       = var.azure_storage_connection_string
      "AppInsightsInstrumentationKey" = var.app_insights_instrumentation_key
    }
  }

  tags = local.common_tags
}

resource "azurerm_container_group" "webforms" {
  name                = "aci-webforms-${local.name_suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Windows"
  ip_address_type     = "Public"
  dns_name_label      = "eshop-webforms-${local.name_suffix}"
  restart_policy      = "Always"

  image_registry_credential {
    server   = azurerm_container_registry.main.login_server
    username = azurerm_container_registry.main.admin_username
    password = azurerm_container_registry.main.admin_password
  }

  container {
    name   = "eshop-webforms"
    image  = "${azurerm_container_registry.main.login_server}/eshop/modernizedwebforms:${var.webforms_image_tag}"
    cpu    = var.container_cpu
    memory = var.container_memory_gb

    ports {
      port     = 80
      protocol = "TCP"
    }

    environment_variables = {
      "UseMockData"             = tostring(var.use_mock_data)
      "UseCustomizationData"    = "False"
      "UseAzureStorage"         = tostring(var.use_azure_storage)
      "UseAzureActiveDirectory" = "false"
    }

    secure_environment_variables = {
      "CatalogDBContext"              = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.catalog.name};User Id=${var.sql_admin_login};Password=${var.sql_admin_password};Encrypt=true;TrustServerCertificate=false;"
      "StorageConnectionString"       = var.azure_storage_connection_string
      "AppInsightsInstrumentationKey" = var.app_insights_instrumentation_key
    }
  }

  tags = local.common_tags
}
