#!/usr/bin/env pwsh
# ─────────────────────────────────────────────────────────────
#  Tollgate build script (Windows PowerShell)
#  Builds the solution and packs NuGet packages into ./artifacts/nuget/
# ─────────────────────────────────────────────────────────────
param(
    [string]$Config = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSCommandPath

Write-Host "==> Restoring..." -ForegroundColor Cyan
dotnet restore "$root/Tollgate.slnx"

Write-Host "==> Building ($Config)..." -ForegroundColor Cyan
dotnet build "$root/Tollgate.slnx" -c $Config --no-restore

Write-Host "==> Packing NuGet packages..." -ForegroundColor Cyan
$nugetPath = "$root/artifacts/nuget"
New-Item -ItemType Directory -Force -Path $nugetPath | Out-Null

dotnet pack "$root/src/Tollgate.Abstractions/Tollgate.Abstractions.csproj" `
    -c $Config --no-build -o $nugetPath

dotnet pack "$root/Tollgate.Licensing/Tollgate.Licensing.csproj" `
    -c $Config --no-build -o $nugetPath

dotnet pack "$root/src/Tollgate.AspNetCore/Tollgate.AspNetCore.csproj" `
    -c $Config --no-build -o $nugetPath

Write-Host ""
Write-Host "✓ Done!" -ForegroundColor Green
Write-Host "  NuGet packages: $nugetPath"
