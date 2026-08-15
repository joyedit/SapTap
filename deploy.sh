#!/bin/bash

# --- VARIABLES ---
MOD_NAME="SapTap"
MODS_PATH="$HOME/.config/VintagestoryData/Mods"
STAGING_DIR="bin/staging"

echo "--- 1. CLEANING ---"
dotnet clean
rm -rf "$STAGING_DIR"
rm -f "$MOD_NAME.zip"

echo "--- 2. BUILDING ---"
dotnet build

echo "--- 3. PACKAGING ---"
# Create a temporary staging area
mkdir -p "$STAGING_DIR"

# Copy essential files
cp modinfo.json "$STAGING_DIR/"
cp bin/Debug/Mods/SapTap/SapTap.dll "$STAGING_DIR/"
cp -r assets "$STAGING_DIR/"

# Zip the contents of the staging directory
cd "$STAGING_DIR"
zip -r -q ../../"$MOD_NAME.zip" *
cd ../..

echo "--- 4. DEPLOYING ZIP ---"
# Move the zip to the game folder
mv "$MOD_NAME.zip" "$MODS_PATH/"

echo "Deploy Complete: $MODS_PATH/$MOD_NAME.zip"
