# eShopLegacyMVC Migration Analysis

## Overview
This document provides a comprehensive analysis of the existing eShopLegacyMVC application structure and configuration to support migration from .NET Framework 4.7.2 to .NET Core/.NET 8.

## Current Application Architecture

### Target Framework Configuration
- **Current Framework**: .NET Framework 4.7.2
- **Project Type**: ASP.NET MVC 5 Web Application
- **Project File Format**: Legacy .csproj format with MSBuild imports
- **Build Tools**: MSBuild with Visual Studio integration

### Core Dependencies Analysis

#### ASP.NET MVC Stack
- **Microsoft.AspNet.Mvc**: 5.2.7
- **Microsoft.AspNet.Razor**: 3.2.7  
- **Microsoft.AspNet.WebPages**: 3.2.7
- **Microsoft.AspNet.Web.Optimization**: 1.1.3 (bundling and minification)

#### Entity Framework Data Access
- **EntityFramework**: 6.2.0
- **Connection String**: Uses SQL Server LocalDB with integrated security
- **Database**: Microsoft.eShopOnContainers.Services.CatalogDb
- **Models**: CatalogItem, CatalogBrand, CatalogType with fluent API configuration

#### Dependency Injection
- **Autofac**: 4.9.1 (upgraded from packages.config version)
- **Autofac.Mvc5**: 4.0.2
- **Autofac.Integration.WebApi**: For Web API controllers
- **Registration**: ApplicationModule with conditional mock/real service registration

#### Logging and Monitoring
- **log4net**: 2.0.10
- **Microsoft.ApplicationInsights**: 2.9.1 with full telemetry stack
- **Custom Logging**: ActivityIdHelper and WebRequestInfo for correlation

#### Frontend Dependencies
- **jQuery**: 3.5.0
- **jQuery.Validation**: 1.19.4
- **bootstrap**: 4.3.1
- **Modernizr**: 2.8.3

### Service Layer Architecture

#### ICatalogService Interface
```csharp
public interface ICatalogService : IDisposable
{
    CatalogItem FindCatalogItem(int id);
    IEnumerable<CatalogBrand> GetCatalogBrands();
    PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex);
    IEnumerable<CatalogType> GetCatalogTypes();
    void CreateCatalogItem(CatalogItem catalogItem);
    void UpdateCatalogItem(CatalogItem catalogItem);
    void RemoveCatalogItem(CatalogItem catalogItem);
}
```

#### Service Implementations
- **CatalogService**: Production implementation using Entity Framework 6
- **CatalogServiceMock**: In-memory mock implementation for development/testing
- **Conditional Registration**: Based on `UseMockData` configuration flag

### Entity Framework Models

#### CatalogItem Entity
- **Primary Key**: Id (manually assigned via HiLo generator)
- **Properties**: Name, Description, Price, PictureFileName, AvailableStock, etc.
- **Relationships**: Many-to-one with CatalogBrand and CatalogType
- **Validation**: Data annotations for price, stock levels, and required fields

#### Database Configuration
- **DbContext**: CatalogDBContext with fluent API configuration
- **Tables**: Catalog, CatalogBrand, CatalogType
- **Sequences**: HiLo sequences for ID generation (catalog_hilo, catalog_brand_hilo, catalog_type_hilo)
- **Initializer**: CatalogDBInitializer with seed data from CSV files

### Configuration System

#### Web.config Structure
- **Connection Strings**: SQL Server LocalDB configuration
- **App Settings**: UseMockData, UseCustomizationData feature flags
- **Entity Framework**: Provider configuration and connection factory
- **HTTP Modules**: Application Insights and telemetry correlation
- **Assembly Binding**: Version redirects for NuGet packages

#### Dependency Injection Configuration
- **Container Registration**: Autofac ContainerBuilder in Global.asax.cs
- **Lifetime Scopes**: InstancePerLifetimeScope for services, SingleInstance for generators
- **MVC Integration**: AutofacDependencyResolver for controllers
- **Web API Integration**: AutofacWebApiDependencyResolver for API controllers

### Controller Architecture

#### CatalogController Pattern
- **Base Class**: System.Web.Mvc.Controller
- **Dependency Injection**: Constructor injection of ICatalogService
- **Actions**: Standard CRUD operations (Index, Details, Create, Edit, Delete)
- **View Models**: PaginatedItemsViewModel for paging support
- **Logging**: log4net integration with method-level logging

### Application Startup

#### Global.asax.cs Configuration
- **Container Registration**: Autofac setup with ApplicationModule
- **MVC Configuration**: Areas, filters, routes, bundles
- **Database Initialization**: Conditional based on mock data flag
- **Session Management**: Machine name and start time tracking

### Static Content and Assets

#### File Structure
- **Content/**: CSS files including Bootstrap 4.3.1
- **Scripts/**: JavaScript libraries and frameworks
- **Images/**: Application branding and UI assets
- **Pics/**: Product catalog images (1.png through 12.png)
- **Setup/**: CSV files for seed data (CatalogBrands.csv, CatalogItems.csv, CatalogTypes.csv)

## Key Migration Considerations

### Framework Dependencies
- **System.Web**: Extensive usage requiring replacement with ASP.NET Core equivalents
- **HttpContext**: Legacy API usage needs updating to ASP.NET Core patterns
- **Web.config**: Complete configuration system replacement needed

### Entity Framework Migration
- **DbContext**: Fluent API syntax changes from EF6 to EF Core
- **Connection Strings**: Configuration pattern changes
- **Migrations**: EF Core migration system vs EF6 automatic migrations

### Dependency Injection Changes
- **Container Integration**: Autofac integration with ASP.NET Core DI container
- **Lifetime Management**: Scoped vs InstancePerLifetimeScope mapping
- **Service Registration**: Integration with IServiceCollection

### Configuration System Migration
- **appsettings.json**: Replace Web.config app settings
- **IConfiguration**: Replace ConfigurationManager
- **Options Pattern**: Strongly-typed configuration classes

This analysis provides the foundation for creating a comprehensive migration plan to .NET Core/.NET 8 while preserving the existing business logic and architectural patterns.
