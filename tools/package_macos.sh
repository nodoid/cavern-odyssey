#!/usr/bin/env bash
# Builds a Mac App Store signed "Cavern Odyssey.app" for osx-arm64 or osx-x64 and
# wraps it in a signed installer package in releases/.
#
# Needs, in the login keychain: "Apple Distribution" (app signing) and
# "3rd Party Mac Developer Installer" (package signing), plus the Mac App Store
# provisioning profile for uk.co.all-the-johnsons.supersqij.
set -euo pipefail
cd "$(dirname "$0")/.."

ARCH="${1:-osx-arm64}"
VERSION="1.0"
BUILD="2"
BUNDLE_ID="uk.co.all-the-johnsons.supersqij"
PROFILE_NAME="${PROFILE_NAME:-rel-supersqij-mac}"
SIGN_ID="${SIGN_ID:-Apple Distribution}"
INSTALLER_SIGN_ID="${INSTALLER_SIGN_ID:-3rd Party Mac Developer Installer}"
ENTITLEMENTS="packaging/macos/CavernOdyssey.entitlements"

APP="build/macos/$ARCH/Cavern Odyssey.app"
BIN="$APP/Contents/MacOS"
RES="$APP/Contents/Resources"
ICONSET="build/AppIcon-$ARCH.iconset"

# Locate the release provisioning profile: as downloaded, or installed by Xcode.
PROFILE=""
for dir in "$HOME/Downloads" \
           "$HOME/Library/MobileDevice/Provisioning Profiles" \
           "$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles"; do
  for f in "$dir"/*; do
    case "$f" in *.mobileprovision|*.provisionprofile) ;; *) continue ;; esac
    [ -e "$f" ] || continue
    name=$(security cms -D -i "$f" 2>/dev/null | plutil -extract Name raw - 2>/dev/null || true)
    if [ "$name" = "$PROFILE_NAME" ]; then PROFILE="$f"; fi
  done
done
if [ -z "$PROFILE" ]; then
  echo "error: provisioning profile '$PROFILE_NAME' not found." >&2
  echo "       Download it from developer.apple.com (keep it in ~/Downloads), or sign the" >&2
  echo "       app for the team in Xcode once so it lands in the Xcode profile folder." >&2
  exit 1
fi

rm -rf "$APP" "$ICONSET"
mkdir -p "$BIN" "$RES" "$ICONSET"

dotnet publish src/SuperSqij.DesktopGL/SuperSqij.DesktopGL.csproj \
  -c Release -r "$ARCH" --self-contained true \
  -p:DebugType=None -p:DebugSymbols=false \
  -o "$BIN" -v q --nologo

# createdump is the .NET crash-dump helper. It ships as a separate Mach-O
# executable and App Store validation requires every executable in the bundle to
# carry com.apple.security.app-sandbox; the tool is optional, so drop it.
rm -f "$BIN/createdump"

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
    <key>CFBundleIdentifier</key><string>${BUNDLE_ID}</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>CFBundleName</key><string>Cavern Odyssey</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>${VERSION}</string>
    <key>CFBundleVersion</key><string>${BUILD}</string>
    <key>LSApplicationCategoryType</key><string>public.app-category.games</string>
    <key>LSMinimumSystemVersion</key><string>12.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>NSSupportsAutomaticGraphicsSwitching</key><true/>
    <key>NSHumanReadableCopyright</key><string>Copyright © 2026 Paul F. Johnson.</string>
</dict>
</plist>
PLIST
plutil -lint "$APP/Contents/Info.plist" >/dev/null

# The embedded profile is what ties the signature to the Mac App Store. The copy
# keeps the profile's extended attributes (the one downloaded from the developer
# site is quarantine-flagged) and the attributes end up in the pkg payload, which
# App Store validation rejects with error 91109; strip them.
cp "$PROFILE" "$APP/Contents/embedded.provisionprofile"
xattr -c "$APP/Contents/embedded.provisionprofile"

# The profile grants the app its identity entitlements (application-identifier and
# team-identifier); without them the sandbox has nothing to bind to. Merge the
# profile's entitlements with the sandbox ones we ask for.
mkdir -p "build/macos/$ARCH"
security cms -D -i "$PROFILE" > "build/macos/$ARCH/profile.plist"
python3 - "$ENTITLEMENTS" "build/macos/$ARCH/profile.plist" "build/macos/$ARCH/entitlements.merged.plist" <<'PY'
import plistlib, sys
wanted = plistlib.load(open(sys.argv[1], "rb"))
profile = plistlib.load(open(sys.argv[2], "rb"))["Entitlements"]
merged = dict(profile)
merged.pop("get-task-allow", None)
merged.update(wanted)
plistlib.dump(merged, open(sys.argv[3], "wb"))
PY

# .NET publishes managed assemblies as executable files, and codesign treats
# anything executable inside Contents/MacOS as nested code it must sign. Drop the
# bit on everything that is not Mach-O so only the real binaries get signed.
for f in "$BIN"/*; do
  if file -b "$f" | grep -qE "Mach-O|PE32"; then chmod +x "$f"; else chmod a-x "$f"; fi
done

# Belt and braces: published files can carry xattrs too, and any of them would
# fail validation, so clear the whole bundle before anything is signed.
xattr -cr "$APP"

# codesign wants every file inside Contents/MacOS signed (it counts the managed
# PE assemblies and even the runtime config as nested code), so sign all of them
# and then the bundle. The bundle's main executable is skipped: a path to it
# means "sign the whole bundle", which only works once the rest is done.
for f in "$BIN"/*; do
  [ -f "$f" ] || continue
  [ "$f" = "$BIN/SuperSqij" ] && continue
  codesign --force --timestamp --sign "$SIGN_ID" "$f" >/dev/null
done
codesign --force --timestamp \
  --entitlements "build/macos/$ARCH/entitlements.merged.plist" \
  --sign "$SIGN_ID" "$APP"
codesign --verify --deep --strict --verbose=2 "$APP"

mkdir -p releases
PKG="releases/CavernOdyssey-${VERSION}-macos-${ARCH#osx-}.pkg"
rm -f "$PKG"
productbuild --component "$APP" /Applications \
  --sign "$INSTALLER_SIGN_ID" "$PKG" >/dev/null
xattr -c "$PKG"
pkgutil --check-signature "$PKG"
echo "built: $PKG"
