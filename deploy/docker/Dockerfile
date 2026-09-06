# ─────────────────────────────────────────────────────────────
#  Tollgate Server — Docker image
#
#  Build:
#      docker build -t tollgate-server:1.0 .
#
#  Run (replace secrets):
#      docker run -d -p 7431:7431 \
#        -e Jwt__Secret="REPLACE_WITH_LONG_RANDOM_SECRET" \
#        -e Admin__Key="REPLACE_WITH_ADMIN_PASSWORD" \
#        -e Cors__AllowedOrigins__0="https://app.yourdomain.com" \
#        -v tollgate-data:/data \
#        tollgate-server:1.0
# ─────────────────────────────────────────────────────────────

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 7431

# ── Build stage ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy central build files first (affects restore + layer caching).
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY icon.png ./

# Copy csproj files for layer caching.
COPY src/Tollgate.Abstractions/Tollgate.Abstractions.csproj ./src/Tollgate.Abstractions/
COPY src/Tollgate.Licensing/Tollgate.Licensing.csproj         ./src/Tollgate.Licensing/
COPY src/Tollgate.AspNetCore/Tollgate.AspNetCore.csproj       ./src/Tollgate.AspNetCore/
COPY src/Tollgate.Server/Tollgate.Server.csproj               ./src/Tollgate.Server/
RUN dotnet restore ./src/Tollgate.Server/Tollgate.Server.csproj

# Copy everything else
COPY . .

# Build + publish
RUN dotnet publish ./src/Tollgate.Server/Tollgate.Server.csproj \
    -c Release -o /app/publish \
    /p:UseAppHost=false

# ── Runtime stage ───────────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# curl is not included in the aspnet base image but is needed by the
# docker-compose healthcheck — install it in one lean layer.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV DOTNET_PRINT_TELEMETRY_MESSAGE=false
ENV ASPNETCORE_URLS=http://0.0.0.0:7431

# SQLite database location — matches the tollgate-data volume below.
# (The server also auto-detects /data, this makes it explicit.)
ENV Data__Path=/data/licenses.db

VOLUME ["/data"]

ENTRYPOINT ["dotnet", "Tollgate.Server.dll"]
