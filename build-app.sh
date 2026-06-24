#!/bin/bash
# Builds MacOnScreenChat into a proper .app bundle (required for the GUI,
# menu-bar item, and global hotkey to work). Usage: ./build-app.sh [debug|release]
set -euo pipefail

CONFIG="${1:-debug}"
APP="MacOnScreenChat.app"

swift build -c "$CONFIG"
BIN_PATH="$(swift build -c "$CONFIG" --show-bin-path)/MacOnScreenChat"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS"
cp "$BIN_PATH" "$APP/Contents/MacOS/MacOnScreenChat"
cp Packaging/Info.plist "$APP/Contents/Info.plist"

# Propagate the local .env (secrets) to where the bundled app reads it.
# NOT copied into the bundle, so the key is never embedded or shared.
APPSUP="$HOME/Library/Application Support/MacOnScreenChat"
mkdir -p "$APPSUP"
if [ -f .env ]; then
    cp .env "$APPSUP/.env"
    echo "🔑 Copied .env -> $APPSUP/.env"
fi

echo "✅ Built $APP"
echo "   Launch with:  open $APP"
