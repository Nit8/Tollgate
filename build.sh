#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────
#  Tollgate build script (Linux / macOS)
#  Builds the solution and packs NuGet packages into ./artifacts/nuget/
# ─────────────────────────────────────────────────────────────
set -e

CONFIG="${1:-Release}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "==> Restoring..."
dotnet restore "$ROOT/Tollgate.slnx"

echo "==> Building ($CONFIG)..."
dotnet build "$ROOT/Tollgate.slnx" -c "$CONFIG" --no-restore

echo "==> Packing NuGet packages..."
NUGET_PATH="$ROOT/artifacts/nuget"
mkdir -p "$NUGET_PATH"

dotnet pack "$ROOT/src/Tollgate.Abstractions/Tollgate.Abstractions.csproj" \
    -c "$CONFIG" --no-build -o "$NUGET_PATH"

dotnet pack "$ROOT/src/Tollgate.Licensing/Tollgate.Licensing.csproj" \
    -c "$CONFIG" --no-build -o "$NUGET_PATH"

dotnet pack "$ROOT/src/Tollgate.AspNetCore/Tollgate.AspNetCore.csproj" \
    -c "$CONFIG" --no-build -o "$NUGET_PATH"

echo ""
echo "✓ Done!"
echo "  NuGet packages: $NUGET_PATH"
