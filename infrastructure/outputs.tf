output "resource_group_name" {
  description = "Name of the Azure resource group"
  value       = azurerm_resource_group.main.name
}

output "acr_login_server" {
  description = "Login server URL for Azure Container Registry"
  value       = azurerm_container_registry.main.login_server
}

output "acr_admin_username" {
  description = "Admin username for Azure Container Registry"
  value       = azurerm_container_registry.main.admin_username
  sensitive   = true
}

output "mvc_app_fqdn" {
  description = "FQDN for the MVC application container"
  value       = azurerm_container_group.mvc.fqdn
}

output "mvc_app_url" {
  description = "URL for the MVC application"
  value       = "http://${azurerm_container_group.mvc.fqdn}"
}

output "webforms_app_fqdn" {
  description = "FQDN for the WebForms application container"
  value       = azurerm_container_group.webforms.fqdn
}

output "webforms_app_url" {
  description = "URL for the WebForms application"
  value       = "http://${azurerm_container_group.webforms.fqdn}"
}

output "sql_server_fqdn" {
  description = "FQDN of the Azure SQL Server"
  value       = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "sql_database_name" {
  description = "Name of the Azure SQL Database"
  value       = azurerm_mssql_database.catalog.name
}

output "vnet_id" {
  description = "ID of the virtual network"
  value       = azurerm_virtual_network.main.id
}

output "sql_connection_string" {
  description = "ADO.NET connection string for the SQL database"
  value       = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.catalog.name};User Id=${var.sql_admin_login};Password=${var.sql_admin_password};Encrypt=true;TrustServerCertificate=false;"
  sensitive   = true
}
