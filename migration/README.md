# .NET Framework 4.7.2 -> .NET 8 migration (eShopLegacyMVC + eShopLegacyWebForms)

Integration branch for the dynamic-workflow migration. Ticket PRs target `devin/net8-eshop-migration`; the umbrella PR targets `main` and stays a draft. Nothing merges into `main`.

## Scope
- Port `eShopLegacyMVCSolution/src/eShopLegacyMVC` (ASP.NET MVC 5 + Web API 2, EF6, Autofac, log4net) to ASP.NET Core on .NET 8 with EF Core 8 and built-in DI.
- Port the `eShopLegacyWebFormsSolution` catalog pages (list/detail/create/edit/delete) to .NET 8 if budget allows.
- Preserve the catalog list/detail/create/edit/delete flows and the `UseMockData` toggle.
- Containerise on Linux (`mcr.microsoft.com/dotnet/aspnet:8.0`).

## Legacy runtime on this Linux estate
.NET Framework needs Windows; the Devin VM is Ubuntu, so the legacy apps run on **Mono 6.12 + xsp4**:

```bash
cp -r eShopLegacyMVCSolution /tmp/legacy && cd /tmp/legacy
nuget restore src/eShopLegacyMVC/packages.config -PackagesDirectory packages
msbuild src/eShopLegacyMVC/eShopLegacyMVC.csproj -restore /p:Configuration=Debug
cd src/eShopLegacyMVC
sed -i 's/key="UseMockData" value="false"/key="UseMockData" value="true"/' Web.config
sed -i 's/System.Web.Mvc, Version=5.2.3.0/System.Web.Mvc, Version=5.2.7.0/g' Views/Web.config   # Mono does not apply the binding redirect to the Razor host factory
xsp4 --port 5100 --nonstop
```

Harness-only config edits (in a throwaway copy, never committed): `UseMockData=true` (no LocalDB on Linux) and the Razor host factory version.
