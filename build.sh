#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────
#  Tollgate build script (Linux / macOS)
#  Builds the solution, runs the tests, and packs the NuGet
#  packages into ./artifacts/nuget/ — then verifies all expected
#  packages actually exist before reporting success.
# ─────────────────────────────────────────────────────────────
set -euo pipefail

CONFIG="${1:-Release}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SLN="$ROOT/Tollgate.slnx"

echo "==> Restoring..."
dotnet restore "$SLN"

echo "==> Building ($CONFIG)..."
dotnet build "$SLN" -c "$CONFIG" --no-restore

echo "==> Running tests..."
dotnet test "$SLN" -c "$CONFIG" --no-build

echo "==> Packing NuGet packages..."
NUGET_PATH="$ROOT/artifacts/nuget"
mkdir -p "$NUGET_PATH"

dotnet pack "$ROOT/src/Tollgate.Abstractions/Tollgate.Abstractions.csproj" \
    -c "$CONFIG" --no-build -o "$NUGET_PATH"

dotnet pack "$ROOT/src/Tollgate.Licensing/Tollgate.Licensing.csproj" \
    -c "$CONFIG" --no-build -o "$NUGET_PATH"

dotnet pack "$ROOT/src/Tollgate.AspNetCore/Tollgate.AspNetCore.csproj" \
    -c "$CONFIG" --no-build -o "$NUGET_PATH"

# Optional: also pack the KeyGen CLI as a .NET global tool.
if [[ "${PACK_KEYGEN:-0}" == "1" ]]; then
    dotnet pack "$ROOT/src/Tollgate.KeyGen/Tollgate.KeyGen.csproj" \
        -c "$CONFIG" -o "$NUGET_PATH"
fi

echo ""
echo "==> Verifying package output..."
# Portable sed (BSD grep on macOS lacks -P)
VERSION="$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$ROOT/Directory.Build.props" | head -n 1)"
MISSING=0
for pkg in Tollgate.Abstractions Tollgate.Licensing Tollgate.AspNetCore; do
    if ls "$NUGET_PATH/${pkg}.${VERSION}.nupkg" >/dev/null 2>&1; then
        echo "  OK  ${pkg}.${VERSION}.nupkg"
    else
        echo "  MISSING  ${pkg}.${VERSION}.nupkg" >&2
        MISSING=1
    fi
done
if [[ "$MISSING" != "0" ]]; then
    echo "Package verification FAILED — not all packages were produced." >&2
    exit 1
fi

echo ""
echo "Done."
echo "  NuGet packages: $NUGET_PATH"
echo "  Version:        $VERSION"
