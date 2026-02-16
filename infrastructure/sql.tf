resource "azurerm_mssql_server" "main" {
  name                         = "sql-${local.name_suffix}"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"

  tags = local.common_tags
}

resource "azurerm_mssql_database" "catalog" {
  name      = "Microsoft.eShopOnContainers.Services.CatalogDb"
  server_id = azurerm_mssql_server.main.id
  sku_name  = var.sql_sku_name

  max_size_gb = 2

  short_term_retention_policy {
    retention_days = 7
  }

  tags = local.common_tags
}

resource "azurerm_private_endpoint" "sql" {
  name                = "pe-sql-${local.name_suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  subnet_id           = azurerm_subnet.sql.id

  private_service_connection {
    name                           = "psc-sql-${local.name_suffix}"
    private_connection_resource_id = azurerm_mssql_server.main.id
    subresource_names              = ["sqlServer"]
    is_manual_connection           = false
  }

  tags = local.common_tags
}

resource "azurerm_private_dns_zone" "sql" {
  name                = "privatelink.database.windows.net"
  resource_group_name = azurerm_resource_group.main.name

  tags = local.common_tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "sql" {
  name                  = "vnet-link-sql"
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.sql.name
  virtual_network_id    = azurerm_virtual_network.main.id
}

resource "azurerm_private_dns_a_record" "sql" {
  name                = azurerm_mssql_server.main.name
  zone_name           = azurerm_private_dns_zone.sql.name
  resource_group_name = azurerm_resource_group.main.name
  ttl                 = 300
  records             = [azurerm_private_endpoint.sql.private_service_connection[0].private_ip_address]
}
