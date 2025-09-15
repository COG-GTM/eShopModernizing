FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["eShopModernized.csproj", "."]
RUN dotnet restore "./eShopModernized.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "eShopModernized.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "eShopModernized.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "eShopModernized.dll"]
