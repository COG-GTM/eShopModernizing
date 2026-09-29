FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    HEALTHCHECK_PATH=/health/live \
    Logging__Console__FormatterName=json
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG APP_VERSION=1.0.0
WORKDIR /src
COPY ["eShopModernized.csproj", "."]
RUN dotnet restore "./eShopModernized.csproj"
COPY . .
RUN dotnet publish "eShopModernized.csproj" -c Release -o /app/publish --no-restore \
    -p:UseAppHost=false -p:InformationalVersion=${APP_VERSION}

FROM base AS final
ARG APP_VERSION=1.0.0
ENV Deployment__Version=${APP_VERSION}
COPY --from=build /app/publish .
USER $APP_UID
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=3 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/${ASPNETCORE_HTTP_PORTS%%;*} && printf 'GET %s HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' \"$HEALTHCHECK_PATH\" >&3 && head -n1 <&3 | grep -q ' 200 '"]
ENTRYPOINT ["dotnet", "eShopCoreModernized.dll"]
