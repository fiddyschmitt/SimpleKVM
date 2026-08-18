#!/bin/bash
# Assembles SimpleKVM.app from a dotnet publish folder, ad-hoc signs it, and zips it.
#
# Usage: package-macos.sh <publish-dir> [output-dir]
#
# <publish-dir> is the output of:
#   dotnet publish SimpleKVM/SimpleKVM.csproj -f net10.0 -r osx-arm64 --self-contained -c Release
# Run this script on a Mac (needs codesign; iconutil/sips for the icon).

set -euo pipefail

PUBLISH_DIR="${1:?Usage: package-macos.sh <publish-dir> [output-dir]}"
OUT_DIR="${2:-.}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

APP="$OUT_DIR/SimpleKVM.app"
ICO="$REPO_ROOT/SimpleKVM/iconfinder_Communication_pc_computer_sharing_6588768_white_bg.ico"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# Stamp the app version from the csproj so there is a single source of truth
VERSION="$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' "$REPO_ROOT/SimpleKVM/SimpleKVM.csproj" | head -1)"
sed "s|@VERSION@|${VERSION:-0.0.0}|g" "$SCRIPT_DIR/../packaging/macos/Info.plist" > "$APP/Contents/Info.plist"
cp -R "$PUBLISH_DIR/." "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/SimpleKVM"

# Icon: use a prebuilt icns when present, otherwise derive one from the Windows .ico
if [ -f "$SCRIPT_DIR/../packaging/macos/SimpleKVM.icns" ]; then
    cp "$SCRIPT_DIR/../packaging/macos/SimpleKVM.icns" "$APP/Contents/Resources/SimpleKVM.icns"
elif [ -f "$ICO" ]; then
    ICONSET="$(mktemp -d)/SimpleKVM.iconset"
    mkdir -p "$ICONSET"
    if sips -s format png "$ICO" --out "$ICONSET/icon_512x512.png" >/dev/null 2>&1; then
        for size in 16 32 128 256 512; do
            sips -z $size $size "$ICONSET/icon_512x512.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
        done
        iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/SimpleKVM.icns"
    else
        echo "warning: could not convert the .ico; the app will use the generic icon"
    fi
fi

codesign --force --deep -s - "$APP"

ZIP="$OUT_DIR/SimpleKVM-macos-arm64.zip"
rm -f "$ZIP"
ditto -c -k --keepParent "$APP" "$ZIP"

echo "Created:"
echo "  $APP"
echo "  $ZIP"
