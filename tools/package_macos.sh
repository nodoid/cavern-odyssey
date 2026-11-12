#!/usr/bin/env bash
# Builds "Cavern Odyssey.app" for osx-arm64 or osx-x64 and zips it into releases/.
set -euo pipefail
cd "$(dirname "$0")/.."

ARCH="${1:-osx-arm64}"
VERSION="1.0"
APP="build/macos/$ARCH/Cavern Odyssey.app"
BIN="$APP/Contents/MacOS"
RES="$APP/Contents/Resources"
ICONSET="build/AppIcon-$ARCH.iconset"

rm -rf "$APP" "$ICONSET"
mkdir -p "$BIN" "$RES" "$ICONSET"

dotnet publish src/SuperSqij.DesktopGL/SuperSqij.DesktopGL.csproj \
  -c Release -r "$ARCH" --self-contained true \
  -p:DebugType=None -p:DebugSymbols=false \
  -o "$BIN" -v q --nologo

# App icon from the generated 1024px source.
SRC_ICON="src/SuperSqij.Shared/Assets/icon_1024.png"
if [ -f "$SRC_ICON" ]; then
  for spec in "16 icon_16x16" "32 icon_16x16@2x" "32 icon_32x32" "64 icon_32x32@2x" \
              "128 icon_128x128" "256 icon_128x128@2x" "256 icon_256x256" \
              "512 icon_256x256@2x" "512 icon_512x512" "1024 icon_512x512@2x"; do
    set -- $spec
    sips -z "$1" "$1" "$SRC_ICON" --out "$ICONSET/$2.png" >/dev/null
  done
  iconutil -c icns "$ICONSET" -o "$RES/AppIcon.icns"
  rm -rf "$ICONSET"
fi

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key><string>en_GB</string>
    <key>CFBundleDisplayName</key><string>Cavern Odyssey</string>
    <key>CFBundleExecutable</key><string>SuperSqij</string>
    <key>CFBundleIconFile</key><string>AppIcon</string>
    <key>CFBundleIdentifier</key><string>uk.co.allthejohnsons.supersqij</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>CFBundleName</key><string>Cavern Odyssey</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>${VERSION}</string>
    <key>CFBundleVersion</key><string>1</string>
    <key>LSApplicationCategoryType</key><string>public.app-category.games</string>
    <key>LSMinimumSystemVersion</key><string>12.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>NSSupportsAutomaticGraphicsSwitching</key><true/>
    <key>NSHumanReadableCopyright</key><string>Copyright © 2026 Paul F. Johnson.</string>
</dict>
</plist>
PLIST

plutil -lint "$APP/Contents/Info.plist" >/dev/null
codesign --force --deep --sign - "$APP" >/dev/null 2>&1 || true

mkdir -p releases
ZIP="releases/CavernOdyssey-${VERSION}-macos-${ARCH#osx-}.zip"
rm -f "$ZIP"
(cd "build/macos/$ARCH" && ditto -c -k --keepParent "Cavern Odyssey.app" "../../../$ZIP")
echo "built: $ZIP"
