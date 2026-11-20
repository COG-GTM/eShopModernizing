# eShop Legacy to .NET Core Migration

This document describes the strangler fig migration of the catalog MVC app (.NET Framework 4.7.2) to ASP.NET Core on .NET 8. The current architecture, with a diagram and routing tables, is in [docs/architecture.md](./docs/architecture.md); the target end state is in [MIGRATION-COMPLETE.md](./MIGRATION-COMPLETE.md).

## Migration Strategy

### Strangler Fig Pattern
- **Phase 1**: Create the ASP.NET Core foundation (`eShopModernized/src/eShopCoreModernized`)
- **Phase 2**: Move features and routes to it one at a time behind nginx
- **Phase 3**: Decommission the .NET Framework apps once all traffic is migrated

### Current Status: Phase 2 implemented, catalog traffic at the 25% stage

| Step | Merged in |
|------|-----------|
| ASP.NET Core foundation, then Azure integrations behind feature flags | PR #2, PR #3, PR #4 |
| Configuration and models | PR #5 |
| Service layer and API controllers | PR #6 |
| Catalog MVC controllers and Razor views | PR #7 |
| nginx routing, 25/50/75/100% traffic stages, `migrate-traffic.sh`, rollback procedures | PR #8 |

The committed `nginx.conf` is identical to `nginx-25percent.conf`: `/api/` goes to ASP.NET Core, `/Catalog/` is split 75% eShopModernizedMVC (port 5001) / 25% ASP.NET Core (port 5002), and everything else goes to eShopModernizedMVC. The 100% catalog cutover described in [MIGRATION-COMPLETE.md](./MIGRATION-COMPLETE.md) is `nginx-100percent.conf`, applied with `./migrate-traffic.sh 100`. Phase 3 (decommissioning) has not been done.

#### What's Been Created:
- `eShopCoreModernized`: ASP.NET Core MVC project targeting .NET 8 (`net8.0`)
- Modern SDK-style project structure
- EF Core migration from Entity Framework 6
- Built-in ASP.NET Core DI replacing Autofac
- Async/await patterns throughout service layer
- Modern logging with ILogger replacing log4net
- Catalog MVC controller and Razor views, with cookie sign-in for create, edit and delete
- API controllers: `api/brands`, `api/files`, `api/imageupload`, `api/health`, `api/health/detailed`
- Azure integrations behind feature flags: Key Vault, Blob Storage, Application Insights, Azure AD, Managed Identity

#### Key Dependency Mappings:
- **EntityFramework 6.2.0** → **Microsoft.EntityFrameworkCore 8.0.8**
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

The `.conf` files at the repo root contain `upstream` and `server` blocks for an `http {}` context (for example `/etc/nginx/conf.d/eshop.conf`); they are not complete nginx configurations.

```nginx
upstream legacy_backend     { server localhost:5000; }  # eShopLegacyMVC, no location routes to it
upstream modernized_backend { server localhost:5001; }  # eShopModernizedMVC (.NET Framework)
upstream core_backend       { server localhost:5002; }  # eShopCoreModernized (.NET 8)

# 25% stage, as committed in nginx.conf
upstream catalog_migration {
    server localhost:5001 weight=3;
    server localhost:5002 weight=1;
}
```

| Location | Upstream |
|----------|----------|
| `/api/` (incl. `/api/health`, `/api/health/detailed`) | `core_backend` |
| `/Catalog/` | `catalog_migration` (weighted) |
| `/` | `modernized_backend` |

### Traffic Stages

| Command | Stage file | `/Catalog/` to ASP.NET Core |
|---------|------------|-----------------------------|
| `./migrate-traffic.sh 25` | `nginx-25percent.conf` | 25% |
| `./migrate-traffic.sh 50` | `nginx-50percent.conf` | 50% |
| `./migrate-traffic.sh 75` | `nginx-75percent.conf` | 75% |
| `./migrate-traffic.sh 100` | `nginx-100percent.conf` | 100% |

The script backs up `nginx.conf` to `nginx-backups/`, copies the stage file over it and polls `http://localhost/api/health` for 5 minutes. It does not reload nginx; copy the file to where nginx includes it and run `sudo nginx -s reload`. To go back, see [ROLLBACK-PROCEDURES.md](./ROLLBACK-PROCEDURES.md).

## Running Both Systems

### .NET Framework system (eShopModernizedMVC)
```bash
# Build eShopModernizedMVCSolution/eShopModernizedMVC.sln with Visual Studio / msbuild on Windows
# nginx expects it on port 5001
```

### ASP.NET Core system (eShopCoreModernized)
```bash
cd eShopModernized/src/eShopCoreModernized/
dotnet run --urls http://localhost:5002
# There is no launchSettings.json, so without --urls Kestrel listens on port 5000
```

### nginx Proxy
```bash
sudo cp nginx.conf /etc/nginx/conf.d/eshop.conf
sudo nginx -t && sudo nginx -s reload
# Access via http://localhost (port 80)
```

## Remaining Work

1. **Finish the catalog cutover**: `./migrate-traffic.sh 100`, then move the remaining routes under `/` off port 5001
2. **Decommission**: retire eShopLegacyMVC (port 5000) and then eShopModernizedMVC (port 5001)
3. **Database**: eventually move to separate databases if needed

## Verification Commands

```bash
# Build the ASP.NET Core project
cd eShopModernized/src/eShopCoreModernized/
dotnet build

# Run it and check health
dotnet run --urls http://localhost:5002 &
curl http://localhost:5002/api/health

# Test the nginx configuration once it is included from an http {} block
sudo nginx -t
```

## Architecture Comparison

| Component | Legacy (.NET Framework) | Modernized (.NET Core) |
|-----------|------------------------|------------------------|
| **Framework** | .NET Framework 4.7.2 | .NET 8 |
| **Project Style** | packages.config | SDK-style |
| **ORM** | Entity Framework 6.2.0 | EF Core 8.0.8 |
| **DI Container** | Autofac 4.9.1 | Built-in ASP.NET Core DI |
| **Logging** | log4net 2.0.10 | ILogger |
| **Hosting** | IIS/IIS Express | Kestrel |
| **Configuration** | Web.config | appsettings.json |
| **Async Patterns** | Limited | async/await throughout |

Both systems maintain the same business logic and database schema for seamless coexistence during migration.
