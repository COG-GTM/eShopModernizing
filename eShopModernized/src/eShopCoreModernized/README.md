# eShopCoreModernized - .NET Core Foundation

This project represents the .NET Core foundation for migrating the eShopLegacyMVC application using a strangler fig approach. It provides a modern ASP.NET Core implementation with comprehensive Azure integrations.

## Features

- **Modern .NET 8 ASP.NET Core MVC application**
- **Comprehensive Azure integrations:**
  - Azure Key Vault for configuration management
  - Azure Blob Storage for image handling
  - Azure Application Insights for telemetry
  - Azure Active Directory for authentication
- **Database compatibility** with existing legacy system
- **Feature flags** for enabling/disabling Azure services
- **Async/await patterns** throughout the application
- **Structured logging** with Microsoft.Extensions.Logging

## Configuration

The application uses `appsettings.json` for configuration with support for Azure Key Vault in production environments.

### Key Configuration Settings

```json
{
  "AppSettings": {
    "UseMockData": false,
    "UseCustomizationData": false,
    "UseAzureStorage": false,
    "UseAzureManagedIdentity": false,
    "UseAzureActiveDirectory": false
  },
  "Azure": {
    "KeyVaultName": "",
    "StorageConnectionString": "",
    "ApplicationInsights": {
      "InstrumentationKey": ""
    },
    "ActiveDirectory": {
      "ClientId": "",
      "TenantId": "",
      "Instance": "https://login.microsoftonline.com/",
      "PostLogoutRedirectUri": "https://localhost:5001/"
    }
  }
}
```

## Azure Service Integration

### Azure Storage
- **ImageAzureStorage**: Handles image uploads and management using Azure Blob Storage
- **ImageMockStorage**: Local development alternative that doesn't require Azure

### Azure Key Vault
- Automatically configured in production environments
- Loads secrets and configuration from Key Vault using Managed Identity

### Azure Active Directory
- OpenID Connect authentication
- Configurable via feature flags
- Supports both development and production scenarios

### Application Insights
- Comprehensive telemetry collection
- Performance monitoring
- Error tracking and diagnostics

## Health Checks and Structured Logging

| Endpoint | Checks | Used by |
|---|---|---|
| `/health/live` | process only | Docker `HEALTHCHECK` (default `HEALTHCHECK_PATH`) |
| `/health/ready` | catalog DB (or mock data), Azure Blob Storage when enabled (degraded, not unhealthy) | compose health checks, `migrate-traffic.sh` |
| `/health`, `/health/detailed` | all checks | operators / dashboards |
| `/api/health`, `/api/health/detailed` | readiness / all checks, legacy response shape | existing consumers |

Endpoints return 200 for Healthy/Degraded and 503 for Unhealthy. Each check times out after 5 seconds.

Deployment metadata comes from the `Deployment` section (`Deployment__ServiceName`, `Deployment__Version`, `Deployment__Track`); the version defaults to the assembly informational version, which the Dockerfile sets from the `APP_VERSION` build argument. The metadata is added to every log scope, to Application Insights (`ApplicationVersion`, `DeploymentTrack`, cloud role name), and to the `X-App-Version` / `X-Deployment-Track` response headers.

Logs are written as JSON to the console outside Development. Each request emits one `RequestCompleted` (EventId 1000) event; successful health probes are logged at Debug. The `X-Correlation-ID` request header is reused when it is at most 128 characters of `[A-Za-z0-9-_.:]`, otherwise the trace ID is used; the value is returned in the response and in ProblemDetails error responses.

Tests: `dotnet test eShopModernized/tests/eShopCoreModernized.Tests`

## Development Setup

1. **Prerequisites:**
   - .NET 8 SDK
   - SQL Server LocalDB (for local development)
   - Visual Studio 2022 or VS Code

2. **Local Development:**
   ```bash
   dotnet restore
   dotnet build
   dotnet run
   ```

3. **Database Setup:**
   - The application uses the same database schema as the legacy system
   - Connection string points to LocalDB by default
   - Entity Framework migrations handle database initialization

## Deployment

### Azure Deployment
1. Configure Azure services (Storage Account, Key Vault, Application Insights, AAD)
2. Update configuration settings in Azure Key Vault or App Service configuration
3. Enable appropriate feature flags
4. Deploy using Azure DevOps, GitHub Actions, or direct deployment

### Feature Flag Configuration
- **Local Development**: Set all Azure flags to `false`, use mock services
- **Azure Development**: Enable Azure Storage and Application Insights
- **Production**: Enable all Azure services including AAD authentication

## Strangler Fig Migration Strategy

This .NET Core application is designed to gradually replace the legacy system:

1. **Database Compatibility**: Uses the same database schema and connection
2. **Incremental Migration**: Can run alongside the legacy system
3. **Traffic Routing**: Use nginx or Azure Application Gateway to route traffic
4. **Feature Parity**: Maintains the same functionality as the modernized .NET Framework version

## Architecture

- **Controllers**: Modern async ASP.NET Core MVC controllers
- **Services**: Business logic layer with both sync and async methods
- **Models**: Entity Framework Core models with nullable reference types
- **Configuration**: Centralized configuration management
- **Dependency Injection**: Built-in ASP.NET Core DI container

## Key Dependencies

- **Microsoft.EntityFrameworkCore**: Data access layer
- **Azure.Storage.Blobs**: Azure Blob Storage integration
- **Azure.Extensions.AspNetCore.Configuration.Secrets**: Key Vault integration
- **Microsoft.ApplicationInsights.AspNetCore**: Telemetry and monitoring
- **Microsoft.Identity.Web**: Azure AD authentication

## Testing

The application includes comprehensive logging and can be tested locally with mock services or against real Azure services depending on configuration.

## Migration from Legacy System

This project maintains compatibility with the existing database and provides the same functionality as the modernized .NET Framework version, enabling a smooth transition using the strangler fig pattern.
