# eShop Legacy to .NET Core Migration

This document describes the strangler fig migration approach for modernizing the eShopLegacyMVC application from .NET Framework 4.7.2 to .NET 6.

## Migration Strategy

### Strangler Fig Pattern
- **Phase 1**: Create new .NET Core foundation (this session)
- **Phase 2**: Gradually migrate individual features/routes
- **Phase 3**: Decommission legacy system once fully migrated

### Current Status: Phase 1 Complete ✅

#### What's Been Created:
- New `eShopModernized` .NET 6 ASP.NET Core MVC project
- Modern SDK-style project structure
- EF Core migration from Entity Framework 6
- Built-in ASP.NET Core DI replacing Autofac
- Async/await patterns throughout service layer
- Modern logging with ILogger replacing log4net

#### Key Dependency Mappings:
- **EntityFramework 6.2.0** → **Microsoft.EntityFrameworkCore 6.0.25**
- **Autofac 4.9.1** → **Built-in ASP.NET Core DI**
- **log4net 2.0.10** → **Microsoft.Extensions.Logging**
- **System.Web.Mvc 5.2.7** → **Microsoft.AspNetCore.Mvc**

#### Database Compatibility:
- ✅ Same connection string: `Microsoft.eShopOnContainers.Services.CatalogDb`
- ✅ Same table names: `Catalog`, `CatalogBrand`, `CatalogType`
- ✅ Same HiLo sequence: `catalog_hilo`
- ✅ Both systems can share the same database safely

## Reverse Proxy Configuration

### nginx Setup (nginx.conf)
```nginx
# Currently routes 100% traffic to legacy system (port 5000)
# Ready for gradual migration to modernized system (port 5001)

upstream legacy_backend {
    server localhost:5000;
}

upstream modernized_backend {
    server localhost:5001;
}

server {
    listen 80;
    server_name localhost;
    
    # Route all traffic to legacy system initially
    location / {
        proxy_pass http://legacy_backend;
        # ... proxy headers
    }
}
```

### Future Migration Routes
To gradually migrate specific routes, add location blocks like:
```nginx
# Example: Migrate catalog listing to new system
location /Catalog {
    proxy_pass http://modernized_backend;
}

# Example: Migrate API endpoints
location /api/ {
    proxy_pass http://modernized_backend;
}
```

## Running Both Systems

### Legacy System (.NET Framework)
```bash
# Run from eShopLegacyMVCSolution/src/eShopLegacyMVC/
# Runs on port 5000 (or IIS Express port)
```

### Modernized System (.NET Core)
```bash
cd eShopModernized/src/eShopModernized/
dotnet run
# Runs on port 5001 by default
```

### nginx Proxy
```bash
# Install nginx and use provided nginx.conf
nginx -c /path/to/nginx.conf
# Access via http://localhost (port 80)
```

## Next Steps (Future Sessions)

1. **Create Views**: Migrate Razor views from legacy system
2. **Add Static Assets**: Copy CSS, JS, images from legacy
3. **Implement Authentication**: If needed
4. **Add API Controllers**: Migrate WebAPI controllers
5. **Performance Testing**: Compare legacy vs modernized
6. **Gradual Route Migration**: Start with low-risk routes
7. **Monitoring & Logging**: Add Application Insights or similar
8. **Database Migration**: Eventually move to separate databases if needed

## Verification Commands

```bash
# Build new project
cd eShopModernized/src/eShopModernized/
dotnet build

# Run new project
dotnet run

# Test nginx configuration
nginx -t -c nginx.conf
```

## Architecture Comparison

| Component | Legacy (.NET Framework) | Modernized (.NET Core) |
|-----------|------------------------|------------------------|
| **Framework** | .NET Framework 4.7.2 | .NET 6 |
| **Project Style** | packages.config | SDK-style |
| **ORM** | Entity Framework 6.2.0 | EF Core 6.0.25 |
| **DI Container** | Autofac 4.9.1 | Built-in ASP.NET Core DI |
| **Logging** | log4net 2.0.10 | ILogger |
| **Hosting** | IIS/IIS Express | Kestrel |
| **Configuration** | Web.config | appsettings.json |
| **Async Patterns** | Limited | async/await throughout |

Both systems maintain the same business logic and database schema for seamless coexistence during migration.
