# syntax=docker/dockerfile:1
# Build context: eShopNet8/   docker build -f src/eShopNet8.Mvc/Dockerfile -t eshopnet8-mvc .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY global.json ./
COPY src/eShop.Catalog.Core/eShop.Catalog.Core.csproj src/eShop.Catalog.Core/
COPY src/eShop.Catalog.Infrastructure/eShop.Catalog.Infrastructure.csproj src/eShop.Catalog.Infrastructure/
COPY src/eShopNet8.Mvc/eShopNet8.Mvc.csproj src/eShopNet8.Mvc/
RUN dotnet restore src/eShopNet8.Mvc/eShopNet8.Mvc.csproj
COPY src/ src/
RUN dotnet publish src/eShopNet8.Mvc/eShopNet8.Mvc.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY docker/healthcheck.sh /usr/local/bin/healthcheck
# appsettings.json sets Urls=http://localhost:520x for local runs; the unprefixed Urls env var outranks it, ASPNETCORE_URLS alone does not.
ENV ASPNETCORE_URLS=http://+:8080 \
    Urls=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=10s --timeout=5s --start-period=60s --retries=6 CMD ["healthcheck", "/health"]
ENTRYPOINT ["dotnet", "eShopNet8.Mvc.dll"]
