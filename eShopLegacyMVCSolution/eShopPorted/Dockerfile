FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["eShopPorted/eShopPorted.csproj", "eShopPorted/"]
COPY ["eShopLegacy.Utilities/eShopLegacy.Utilities.csproj", "eShopLegacy.Utilities/"]
RUN dotnet restore "eShopPorted/eShopPorted.csproj"
COPY . .
WORKDIR "/src/eShopPorted"
RUN dotnet build "eShopPorted.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "eShopPorted.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "eShopPorted.dll"]
