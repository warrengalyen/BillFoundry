# Production image for BillFoundry.Web.
# Build from the repository root:
#   docker build -f src/BillFoundry.Web/Dockerfile -t billfoundry-web .
#
# The published app listens on HTTP port 8080. TLS belongs on a reverse proxy.
# This image does not apply EF Core migrations. Set Database__ApplyMigrationsOnStartup=true
# only when you intentionally want pending migrations applied at process start.
# That option never drops the database.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/BillFoundry.Domain/BillFoundry.Domain.csproj src/BillFoundry.Domain/
COPY src/BillFoundry.Application/BillFoundry.Application.csproj src/BillFoundry.Application/
COPY src/BillFoundry.Infrastructure/BillFoundry.Infrastructure.csproj src/BillFoundry.Infrastructure/
COPY src/BillFoundry.Web/BillFoundry.Web.csproj src/BillFoundry.Web/

RUN dotnet restore src/BillFoundry.Web/BillFoundry.Web.csproj

FROM restore AS publish
COPY src/ src/
RUN dotnet publish src/BillFoundry.Web/BillFoundry.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# No extra packages: the runtime image may not have outbound package-mirror DNS
# during build. HTTP probes belong to Compose/orchestrators hitting /health.
RUN mkdir -p /app/App_Data/organization-logos /app/data-protection-keys \
    && chown -R $APP_UID:$APP_UID /app/App_Data /app/data-protection-keys

COPY --from=publish /app/publish .

USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Database__ApplyMigrationsOnStartup=false \
    DataProtection__KeyPath=/app/data-protection-keys

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=5 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080"]

ENTRYPOINT ["dotnet", "BillFoundry.Web.dll"]
