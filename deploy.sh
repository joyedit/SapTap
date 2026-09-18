#!/bin/bash
set -euo pipefail

# --- VARIABLES ---
MOD_NAME="SapTap"
MODS_PATH="$HOME/.config/VintagestoryData/Mods"
STAGING_DIR="bin/staging"

# Vintage Story expects the mod filename to carry the version, so read it from modinfo
# rather than keeping a second copy of it here.
VERSION=$(python3 -c "import json;print(json.load(open('modinfo.json'))['version'])")
ZIP_NAME="$MOD_NAME-$VERSION.zip"

echo "--- 1. CLEANING ---"
dotnet clean
rm -rf "$STAGING_DIR"
rm -f "$MOD_NAME"-*.zip

echo "--- 2. BUILDING ---"
dotnet build

echo "--- 3. PACKAGING $ZIP_NAME ---"
mkdir -p "$STAGING_DIR"
cp modinfo.json modicon.png "$STAGING_DIR/"
cp "bin/Debug/Mods/$MOD_NAME/$MOD_NAME.dll" "$STAGING_DIR/"
cp -r assets "$STAGING_DIR/"

(cd "$STAGING_DIR" && zip -r -q "../../$ZIP_NAME" .)

echo "--- 4. DEPLOYING ---"
# Clear out older builds so the game does not load two copies of the mod.
rm -f "$MODS_PATH/$MOD_NAME"-*.zip "$MODS_PATH/$MOD_NAME.zip"
mv "$ZIP_NAME" "$MODS_PATH/"

echo "Deploy complete: $MODS_PATH/$ZIP_NAME"
