FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY eShopLegacy.Utilities/eShopLegacy.Utilities.csproj eShopLegacy.Utilities/
COPY eShopPorted/eShopPorted.csproj eShopPorted/
RUN dotnet restore eShopPorted/eShopPorted.csproj

COPY eShopLegacy.Utilities/ eShopLegacy.Utilities/
COPY eShopPorted/ eShopPorted/
RUN dotnet publish eShopPorted/eShopPorted.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "eShopPorted.dll"]
