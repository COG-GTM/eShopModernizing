FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["eShopLegacyMVC.csproj", "."]
RUN dotnet restore "eShopLegacyMVC.csproj"
COPY . .
RUN dotnet build "eShopLegacyMVC.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "eShopLegacyMVC.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "eShopLegacyMVC.dll"]
