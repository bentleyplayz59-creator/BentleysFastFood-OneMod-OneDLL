#!/usr/bin/env bash
# Local build + package, same steps as .github/workflows/build.yml.
# Needs the .NET 6 SDK and a Stardew Valley install (or pass the game folder: ./build.sh "/path/to/Stardew Valley")
set -euo pipefail
cd "$(dirname "$0")"

PROJECT=src/BentleyFastFood.csproj
DLL_NAME=BentleyPlayz.FastFoodUsefulStuff.Smapi.dll
DLL_DIR=src/bin/Release/net6.0

GAME_ARGS=()
if [ "${1:-}" != "" ]; then GAME_ARGS=("-p:GamePath=$1"); fi

dotnet restore "$PROJECT" "${GAME_ARGS[@]}"
dotnet build "$PROJECT" -c Release --no-restore "${GAME_ARGS[@]}"

test -f "$DLL_DIR/$DLL_NAME"

rm -rf release
mkdir -p release/BentleysFastFood/Code
cp Code/manifest.json Code/config.json Code/announcement.json release/BentleysFastFood/Code/
cp "$DLL_DIR/$DLL_NAME" release/BentleysFastFood/Code/
cp -r Content release/BentleysFastFood/Content
rm -rf release/BentleysFastFood/Content/source

DLL_COUNT=$(find release/BentleysFastFood -type f -name "*.dll" | wc -l)
test "$DLL_COUNT" -eq 1

(cd release && zip -r BentleysFastFood.zip BentleysFastFood)
echo "Done: release/BentleysFastFood.zip"
