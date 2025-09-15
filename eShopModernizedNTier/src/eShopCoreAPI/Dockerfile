FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["src/eShopCoreAPI/eShopCoreAPI.csproj", "src/eShopCoreAPI/"]
RUN dotnet restore "src/eShopCoreAPI/eShopCoreAPI.csproj"
COPY . .
WORKDIR "/src/src/eShopCoreAPI"
RUN dotnet build "eShopCoreAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "eShopCoreAPI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "eShopCoreAPI.dll"]
