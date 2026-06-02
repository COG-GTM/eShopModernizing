FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/eShopWCFService/eShopWCFService.csproj", "src/eShopWCFService/"]
RUN dotnet restore "src/eShopWCFService/eShopWCFService.csproj"
COPY . .
WORKDIR "/src/src/eShopWCFService"
RUN dotnet build "eShopWCFService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "eShopWCFService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "eShopWCFService.dll"]
