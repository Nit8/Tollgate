# ─────────────────────────────────────────────────────────────
#  Tollgate Server — Docker image
#
#  Build:
#      docker build -t tollgate-server:1.0 .
#
#  Run (replace secrets):
#      docker run -d -p 5000:5000 \
#        -e Jwt__Secret="REPLACE_WITH_LONG_RANDOM_SECRET" \
#        -e Admin__Key="REPLACE_WITH_ADMIN_PASSWORD" \
#        -v tollgate-data:/data \
#        tollgate-server:1.0
# ─────────────────────────────────────────────────────────────

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000

# ── Build stage ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for layer caching
COPY src/Tollgate.Abstractions/Tollgate.Abstractions.csproj ./src/Tollgate.Abstractions/
COPY Tollgate.Licensing/Tollgate.Licensing.csproj             ./Tollgate.Licensing/
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

ENV DOTNET_PRINT_TELEMETRY_MESSAGE=false
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "Tollgate.Server.dll"]
