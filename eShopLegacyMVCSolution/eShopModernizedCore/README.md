# eShopModernizedCore

This is the modernized version of eShopLegacyMVC, migrated from .NET Framework 4.7.2 to .NET 8.

## Key Changes

### Framework Migration
- **Target Framework**: .NET 8.0 (LTS)
- **Project Format**: Modern SDK-style .csproj
- **Web Framework**: ASP.NET Core MVC

### Dependencies
- **Entity Framework**: Migrated from EF 6.2.0 to EF Core 8.0
- **Dependency Injection**: Autofac integration with ASP.NET Core DI
- **Logging**: Serilog instead of log4net
- **Configuration**: appsettings.json instead of Web.config

### Architecture Improvements
- **Async/Await**: Service methods now use async patterns
- **Nullable Reference Types**: Enabled for better null safety
- **Modern C# Features**: Using latest C# language features
- **Configuration**: Strongly-typed configuration with IOptions pattern

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- SQL Server LocalDB (or SQL Server)

### Running the Application
```bash
cd eShopModernizedCore
dotnet restore
dotnet build
dotnet run
```

### Configuration
- **Development**: Uses mock data by default (UseMockData: true)
- **Production**: Configure connection string in appsettings.json

### Database Setup
The application will automatically create the database when running with real data (UseMockData: false).

## Migration Notes
This project demonstrates the migration patterns from .NET Framework to .NET Core, preserving the original business logic while adopting modern .NET practices.
