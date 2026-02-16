# eShop Legacy MVC Modernization - Demo Walkthrough

## Overview

This demo shows Devin performing tech debt remediation on a legacy .NET Framework 4.7.2 ASP.NET MVC application, upgrading it to .NET 8 with containerization and cloud-readiness artifacts.

---

## Before State: Legacy .NET Framework 4.7.2

**Location:** `eShopLegacyMVCSolution/src/eShopLegacyMVC/`

The original application is a catalog management app built with:

| Component | Legacy Version |
|-----------|---------------|
| Framework | .NET Framework 4.7.2 |
| Web Stack | ASP.NET MVC 5 (`System.Web.Mvc`) |
| Project Format | Legacy `.csproj` (574 lines, XML-heavy) |
| DI Container | Autofac (third-party) |
| ORM | Entity Framework 6 |
| Logging | log4net |
| Config | `Web.config` with XML transforms |
| Hosting | IIS-only, Windows-only |
| Containerization | None |
| Health Checks | None |

**Key pain points:**
- Locked to Windows and IIS
- 574-line `.csproj` with manual assembly references
- `packages.config` for NuGet dependencies
- `Global.asax` application lifecycle
- `System.Web` dependency throughout
- `BinaryFormatter` usage (security vulnerability in modern .NET)
- No container support, no cloud-readiness

---

## What Devin Did

### Step 1: Framework Upgrade (.NET Framework 4.7.2 → .NET 8)

**File:** `eShopPorted/eShopPorted.csproj`

The project file was modernized from a verbose legacy format to the modern SDK-style format:

- **Before:** 34 lines targeting `net461` with ASP.NET Core 2.2 packages, Autofac, log4net, Newtonsoft.Json
- **After:** 17 lines targeting `net8.0` with EF Core 8, built-in health checks, System.Text.Json

Key dependency changes:
| Before | After |
|--------|-------|
| `Autofac` 4.9.1 | Built-in DI (Microsoft.Extensions.DependencyInjection) |
| `Autofac.Extensions.DependencyInjection` 4.4.0 | Removed |
| `log4net` 2.0.10 | Built-in `ILogger<T>` |
| `Microsoft.EntityFrameworkCore` 2.2.6 | `Microsoft.EntityFrameworkCore` 8.0.2 |
| `Newtonsoft.Json` 13.0.2 | `System.Text.Json` 8.0.1 |
| `Microsoft.AspNetCore` 2.2.0 | Built into .NET 8 SDK |
| `eShopLegacy.Utilities` (BinaryFormatter) | Removed (security risk) |

### Step 2: Modern Hosting Model

**File:** `eShopPorted/Program.cs`

Replaced the legacy `WebHost.CreateDefaultBuilder` + `Startup.cs` pattern with the modern minimal hosting model:

- **Before:** Separate `Program.cs` (18 lines) + `Startup.cs` (69 lines) + `ApplicationModule.cs` (33 lines) = 120 lines across 3 files
- **After:** Single `Program.cs` (114 lines) with `WebApplication.CreateBuilder`, inline DI registration, and health check configuration

**Deleted files:**
- `Startup.cs` — merged into `Program.cs`
- `Modules/ApplicationModule.cs` — replaced by built-in DI
- `Views/Web.config` — not needed in ASP.NET Core
- `app.config` — replaced by `appsettings.json`

### Step 3: Dependency Injection Modernization

Replaced third-party Autofac container with built-in `Microsoft.Extensions.DependencyInjection`:

```csharp
// Before (Autofac)
var builder = new ContainerBuilder();
builder.Populate(services);
builder.RegisterModule(new ApplicationModule(useMockData));
ILifetimeScope container = builder.Build();
return new AutofacServiceProvider(container);

// After (built-in DI)
if (useMockData)
    builder.Services.AddSingleton<ICatalogService, CatalogServiceMock>();
else
    builder.Services.AddScoped<ICatalogService, CatalogService>();
```

### Step 4: Logging Modernization

Replaced log4net with built-in `ILogger<T>` across all controllers:

```csharp
// Before (log4net)
private static readonly ILog _log = LogManager.GetLogger(
    System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
_log.Info($"Now loading... /Catalog/Index?pageSize={pageSize}");

// After (ILogger<T>)
private readonly ILogger<CatalogController> _logger;
_logger.LogInformation("Now loading... /Catalog/Index?pageSize={PageSize}", pageSize);
```

Benefits: structured logging with semantic parameter names, no reflection, DI-friendly.

### Step 5: Legacy API Cleanup

**File:** `Controllers/Api/FilesController.cs`

- Removed `BinaryFormatter` usage (marked as a security vulnerability since .NET 5)
- Replaced binary serialization with standard JSON response via `Ok(brands)`
- Removed dependency on `eShopLegacy.Utilities` (.NET Framework 4.6.1 library)

### Step 6: System.Web Removal

Cleaned up legacy `System.Web` references from model files and controllers:

- `Models/CatalogBrand.cs` — removed `System.Web`, `System.Linq`, `System.Collections.Generic` imports
- `Models/Infrastructure/PreconfiguredData.cs` — removed `System.Web`, `System.Linq` imports
- `Controllers/PicController.cs` — replaced `System.Web.Mvc` with `Microsoft.AspNetCore.Mvc`, replaced `HttpStatusCodeResult`/`HttpNotFound` with `BadRequest()`/`NotFound()`

### Step 7: Containerization

**File:** `eShopPorted/Dockerfile`

Created a multi-stage Docker build:

1. **Base stage:** `mcr.microsoft.com/dotnet/aspnet:8.0` — minimal runtime image
2. **Build stage:** `mcr.microsoft.com/dotnet/sdk:8.0` — full SDK for compilation
3. **Publish stage:** Produces optimized release build
4. **Final stage:** Copies only published output into the slim runtime image

Includes a `HEALTHCHECK` directive for container orchestrators.

### Step 8: Docker Compose Orchestration

**File:** `eShopPorted/docker-compose.yml`

Local orchestration with:
- **eshop.mvc.modern** — the modernized app on port 5100
- **sql.data** — SQL Server 2022 on Linux with health checks
- Service dependency management (`depends_on` with `condition: service_healthy`)
- Persistent volume for database data
- Environment-based configuration overrides

### Step 9: Cloud-Readiness Artifacts

**Health check endpoints:**
- `GET /health/ready` — readiness probe (checks app + database connectivity), returns JSON
- `GET /health/live` — liveness probe (lightweight, always returns healthy)

**Environment-based configuration:**
- `appsettings.json` with structured logging levels
- Environment variable overrides via `ConnectionStrings__DefaultConnection`
- `UseMockData` toggle for development without a database

---

## After State: Modern .NET 8

| Component | Modernized Version |
|-----------|-------------------|
| Framework | .NET 8 (cross-platform) |
| Web Stack | ASP.NET Core MVC |
| Project Format | SDK-style `.csproj` (17 lines) |
| DI Container | Built-in (`Microsoft.Extensions.DependencyInjection`) |
| ORM | Entity Framework Core 8 |
| Logging | Built-in `ILogger<T>` (structured logging) |
| Config | `appsettings.json` + environment variables |
| Hosting | Kestrel (cross-platform: Linux, macOS, Windows) |
| Containerization | Multi-stage Dockerfile |
| Orchestration | Docker Compose with health checks |
| Health Checks | `/health/ready` + `/health/live` endpoints |

---

## Try It Yourself

### Run with mock data (no database needed)

```bash
cd eShopLegacyMVCSolution/eShopPorted

# Restore and run
dotnet restore
dotnet run

# App available at http://localhost:5000
# Health checks at http://localhost:5000/health/live and /health/ready
```

### Run with Docker Compose (full stack)

```bash
cd eShopLegacyMVCSolution/eShopPorted

# Build and start all services
docker compose up --build

# App available at http://localhost:5100
# Health: http://localhost:5100/health/ready (JSON response with DB status)
# Health: http://localhost:5100/health/live (lightweight liveness check)
```

### Verify the health check response

```bash
curl http://localhost:5100/health/ready | jq
```

Expected output:
```json
{
  "status": "Healthy",
  "checks": [
    { "name": "self", "status": "Healthy", "description": null },
    { "name": "database", "status": "Healthy", "description": null }
  ],
  "totalDuration": "00:00:00.0051234"
}
```

### Compare before and after

```bash
# Legacy project file: 574 lines of XML
wc -l eShopLegacyMVCSolution/src/eShopLegacyMVC/eShopLegacyMVC.csproj

# Modernized project file: 17 lines
wc -l eShopLegacyMVCSolution/eShopPorted/eShopPorted.csproj

# Legacy app startup: 3 files (Program.cs + Startup.cs + ApplicationModule.cs)
# Modernized app startup: 1 file (Program.cs)
```

---

## Files Changed Summary

| Action | File | What Changed |
|--------|------|-------------|
| Modified | `eShopPorted.csproj` | net461 → net8.0, removed Autofac/log4net/Newtonsoft |
| Modified | `Program.cs` | WebHost → WebApplication, added health checks |
| Deleted | `Startup.cs` | Merged into Program.cs |
| Deleted | `Modules/ApplicationModule.cs` | Replaced by built-in DI |
| Deleted | `Views/Web.config` | Not needed in ASP.NET Core |
| Deleted | `app.config` | Replaced by appsettings.json |
| Modified | `Controllers/CatalogController.cs` | log4net → ILogger, structured logging |
| Modified | `Controllers/PicController.cs` | System.Web.Mvc → ASP.NET Core, ILogger |
| Modified | `Controllers/Api/FilesController.cs` | Removed BinaryFormatter, JSON response |
| Modified | `Models/CatalogBrand.cs` | Removed System.Web imports |
| Modified | `Models/Infrastructure/PreconfiguredData.cs` | Removed System.Web imports |
| Modified | `Views/Shared/_Layout.cshtml` | Updated Startup.StartTime → Program.StartTime |
| Modified | `appsettings.json` | Structured logging, SQL Server config |
| Added | `Views/_ViewImports.cshtml` | ASP.NET Core tag helpers |
| Added | `Dockerfile` | Multi-stage build |
| Added | `docker-compose.yml` | Local orchestration with SQL Server |
| Added | `.dockerignore` | Build optimization |
