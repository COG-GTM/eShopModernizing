# Dependency Mapping: .NET Framework to .NET Core Migration

## Overview
This document provides detailed mappings for migrating eShopLegacyMVC from .NET Framework 4.7.2 to .NET 8, including package replacements, API changes, and configuration updates.

## Framework Migration

### Target Framework
- **From**: .NET Framework 4.7.2
- **To**: .NET 8.0 (LTS)
- **Project SDK**: Microsoft.NET.Sdk.Web (modern SDK-style format)

## Package Dependencies Mapping

### Entity Framework Migration

#### Core Entity Framework
| Legacy (.NET Framework) | Modern (.NET 8) | Notes |
|-------------------------|-----------------|-------|
| `EntityFramework 6.2.0` | `Microsoft.EntityFrameworkCore.SqlServer 8.0.0` | Complete API rewrite |
| `EntityFramework.SqlServer` | `Microsoft.EntityFrameworkCore.SqlServer` | Included in main package |
| N/A | `Microsoft.EntityFrameworkCore.Tools 8.0.0` | For migrations and scaffolding |
| N/A | `Microsoft.EntityFrameworkCore.Design 8.0.0` | Design-time services |

#### Key API Changes
- `System.Data.Entity.DbContext` → `Microsoft.EntityFrameworkCore.DbContext`
- `System.Data.Entity.DbSet<T>` → `Microsoft.EntityFrameworkCore.DbSet<T>`
- `EntityTypeConfiguration<T>` → `IEntityTypeConfiguration<T>`
- `DbModelBuilder` → `ModelBuilder`
- `HasRequired()` → `HasOne().WithMany()`
- `HasForeignKey()` → `HasForeignKey()` (similar but different syntax)

### ASP.NET MVC Migration

#### Core MVC Framework
| Legacy (.NET Framework) | Modern (.NET 8) | Notes |
|-------------------------|-----------------|-------|
| `Microsoft.AspNet.Mvc 5.2.7` | Built into .NET 8 SDK | No separate package needed |
| `Microsoft.AspNet.Razor 3.2.7` | Built into .NET 8 SDK | Razor engine integrated |
| `Microsoft.AspNet.WebPages 3.2.7` | Built into .NET 8 SDK | Web Pages functionality included |
| `Microsoft.AspNet.Web.Optimization 1.1.3` | Built-in bundling or third-party | ASP.NET Core has built-in bundling |

#### Key API Changes
- `System.Web.Mvc.Controller` → `Microsoft.AspNetCore.Mvc.Controller`
- `System.Web.Mvc.ActionResult` → `Microsoft.AspNetCore.Mvc.IActionResult`
- `System.Web.HttpContext` → `Microsoft.AspNetCore.Http.HttpContext`
- `System.Web.Mvc.ViewBag` → `Microsoft.AspNetCore.Mvc.ViewBag` (similar)
- `System.Web.Mvc.SelectList` → `Microsoft.AspNetCore.Mvc.Rendering.SelectList`

### Dependency Injection Migration

#### Autofac Integration
| Legacy (.NET Framework) | Modern (.NET 8) | Notes |
|-------------------------|-----------------|-------|
| `Autofac 4.9.1` | `Autofac.Extensions.DependencyInjection 9.0.0` | Integrates with ASP.NET Core DI |
| `Autofac.Mvc5 4.0.2` | Not needed | ASP.NET Core integration handles MVC |
| `Autofac.Integration.WebApi` | Not needed | ASP.NET Core integration handles Web API |

#### Registration Changes
```csharp
// Legacy (.NET Framework)
var builder = new ContainerBuilder();
builder.RegisterControllers(Assembly.GetExecutingAssembly());
DependencyResolver.SetResolver(new AutofacDependencyResolver(container));

// Modern (.NET 8)
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder => {
    containerBuilder.RegisterModule(new ApplicationModule(useMockData));
});
```

### Logging Migration

#### Logging Framework
| Legacy (.NET Framework) | Modern (.NET 8) | Notes |
|-------------------------|-----------------|-------|
| `log4net 2.0.10` | `Serilog.AspNetCore 8.0.0` | More modern, structured logging |
| N/A | `Serilog.Sinks.Console 5.0.0` | Console output |
| N/A | `Serilog.Sinks.File 5.0.0` | File output |

#### API Changes
```csharp
// Legacy (log4net)
private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
_log.Info($"Now loading... /Catalog/Index");

// Modern (Serilog/ILogger)
private readonly ILogger<CatalogController> _logger;
_logger.LogInformation("Now loading... /Catalog/Index");
```

### Monitoring and Telemetry

#### Application Insights
| Legacy (.NET Framework) | Modern (.NET 8) | Notes |
|-------------------------|-----------------|-------|
| `Microsoft.ApplicationInsights 2.9.1` | `Microsoft.ApplicationInsights.AspNetCore 2.21.0` | ASP.NET Core specific package |
| `Microsoft.ApplicationInsights.Web 2.9.1` | Included in AspNetCore package | Web-specific features included |
| Multiple AI packages | Single AspNetCore package | Simplified package structure |

### JSON Serialization

#### JSON Handling
| Legacy (.NET Framework) | Modern (.NET 8) | Notes |
|-------------------------|-----------------|-------|
| `Newtonsoft.Json 12.0.1` | `Newtonsoft.Json 13.0.3` or `System.Text.Json` | Can use either, Newtonsoft for compatibility |

### Frontend Dependencies

#### Client-Side Libraries
| Legacy (.NET Framework) | Modern (.NET 8) | Notes |
|-------------------------|-----------------|-------|
| `jQuery 3.5.0` | `jQuery 3.7.1` (via LibMan/npm) | Use LibMan or npm for client libraries |
| `bootstrap 4.3.1` | `bootstrap 5.3.0` (via LibMan/npm) | Major version upgrade available |
| `jQuery.Validation 1.19.4` | `jQuery.Validation 1.19.5` (via LibMan/npm) | Client-side validation |

## Configuration System Migration

### Configuration Files
| Legacy (.NET Framework) | Modern (.NET 8) | Notes |
|-------------------------|-----------------|-------|
| `Web.config` | `appsettings.json` | JSON-based configuration |
| `<appSettings>` | `appsettings.json` root | Flat key-value pairs |
| `<connectionStrings>` | `"ConnectionStrings"` section | Nested JSON object |

### Configuration API Changes
```csharp
// Legacy (.NET Framework)
var useMockData = bool.Parse(ConfigurationManager.AppSettings["UseMockData"]);
var connectionString = ConfigurationManager.ConnectionStrings["CatalogDBContext"].ConnectionString;

// Modern (.NET 8)
var useMockData = configuration.GetValue<bool>("UseMockData");
var connectionString = configuration.GetConnectionString("CatalogDBContext");
```

## Application Startup Migration

### Startup Configuration
| Legacy (.NET Framework) | Modern (.NET 8) | Notes |
|-------------------------|-----------------|-------|
| `Global.asax.cs` | `Program.cs` | Single entry point |
| `Application_Start()` | `WebApplication.CreateBuilder()` | Builder pattern |
| Manual route registration | Convention-based routing | Simplified routing |

### HTTP Pipeline
| Legacy (.NET Framework) | Modern (.NET 8) | Notes |
|-------------------------|-----------------|-------|
| HTTP Modules | Middleware | Pipeline-based approach |
| `Application_BeginRequest` | Custom middleware | More flexible |
| IIS integration | Kestrel web server | Cross-platform |

## Breaking Changes and Compatibility Issues

### System.Web Dependencies
- **Issue**: Extensive use of `System.Web` namespace
- **Solution**: Replace with ASP.NET Core equivalents
- **Impact**: High - requires significant code changes

### HttpContext API Changes
- **Issue**: Different HttpContext API surface
- **Solution**: Update to ASP.NET Core HttpContext
- **Impact**: Medium - mostly property name changes

### Model Binding Changes
- **Issue**: Different model binding behavior
- **Solution**: Update binding attributes and validation
- **Impact**: Low - minimal changes needed

### Entity Framework Query Changes
- **Issue**: Some LINQ queries may behave differently
- **Solution**: Test and update queries as needed
- **Impact**: Medium - requires testing

### Dependency Injection Lifetime Changes
- **Issue**: Different lifetime scope semantics
- **Solution**: Map Autofac lifetimes to ASP.NET Core scopes
- **Impact**: Low - mostly configuration changes

## Migration Strategy Recommendations

### Phase 1: Project Structure
1. Create new .NET 8 project with modern SDK format
2. Set up folder structure following ASP.NET Core conventions
3. Configure basic dependency injection and logging

### Phase 2: Data Layer Migration
1. Migrate Entity Framework models to EF Core
2. Update DbContext configuration
3. Test database connectivity and basic operations

### Phase 3: Service Layer Migration
1. Migrate service interfaces and implementations
2. Update dependency injection registration
3. Test service layer functionality

### Phase 4: Controller Migration
1. Migrate MVC controllers to ASP.NET Core
2. Update action methods and return types
3. Test controller functionality

### Phase 5: View and Frontend Migration
1. Migrate Razor views to ASP.NET Core
2. Update client-side library management
3. Test UI functionality

### Phase 6: Configuration and Deployment
1. Migrate configuration from Web.config to appsettings.json
2. Set up logging and monitoring
3. Test deployment scenarios

This mapping provides a comprehensive guide for migrating each component of the eShopLegacyMVC application to modern .NET 8 patterns and practices.
