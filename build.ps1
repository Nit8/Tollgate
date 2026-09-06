#!/usr/bin/env pwsh
# ─────────────────────────────────────────────────────────────
#  Tollgate build script (Windows PowerShell)
#  Builds the solution, runs the tests, and packs the NuGet
#  packages into ./artifacts/nuget/ — then verifies all expected
#  packages actually exist before reporting success.
# ─────────────────────────────────────────────────────────────
param(
    [string]$Config = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSCommandPath
$sln = Join-Path $root "Tollgate.slnx"

Write-Host "==> Restoring..." -ForegroundColor Cyan
dotnet restore $sln

Write-Host "==> Building ($Config)..." -ForegroundColor Cyan
dotnet build $sln -c $Config --no-restore

Write-Host "==> Running tests..." -ForegroundColor Cyan
dotnet test $sln -c $Config --no-build

Write-Host "==> Packing NuGet packages..." -ForegroundColor Cyan
$nugetPath = Join-Path $root "artifacts/nuget"
New-Item -ItemType Directory -Force -Path $nugetPath | Out-Null

dotnet pack (Join-Path $root "src/Tollgate.Abstractions/Tollgate.Abstractions.csproj") `
    -c $Config --no-build -o $nugetPath

dotnet pack (Join-Path $root "src/Tollgate.Licensing/Tollgate.Licensing.csproj") `
    -c $Config --no-build -o $nugetPath

dotnet pack (Join-Path $root "src/Tollgate.AspNetCore/Tollgate.AspNetCore.csproj") `
    -c $Config --no-build -o $nugetPath

# Optional: also pack the KeyGen CLI as a .NET global tool.
if ($env:PACK_KEYGEN -eq "1") {
    dotnet pack (Join-Path $root "src/Tollgate.KeyGen/Tollgate.KeyGen.csproj") `
        -c $Config -o $nugetPath
}

Write-Host ""
Write-Host "==> Verifying package output..." -ForegroundColor Cyan
$version = (Select-String -Path (Join-Path $root "Directory.Build.props") `
    -Pattern '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value
$missing = @()
foreach ($pkg in @("Tollgate.Abstractions", "Tollgate.Licensing", "Tollgate.AspNetCore")) {
    $file = Join-Path $nugetPath "$pkg.$version.nupkg"
    if (Test-Path $file) {
        Write-Host "  OK  $pkg.$version.nupkg"
    }
    else {
        Write-Host "  MISSING  $pkg.$version.nupkg" -ForegroundColor Red
        $missing += $pkg
    }
}
if ($missing.Count -gt 0) {
    throw "Package verification FAILED — not all packages were produced: $($missing -join ', ')"
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  NuGet packages: $nugetPath"
Write-Host "  Version:        $version"
