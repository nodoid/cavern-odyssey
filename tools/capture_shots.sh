#!/usr/bin/env bash
# Development-only: renders the App Store screenshots at exact size, as flat RGB PNGs.
#
#   bash tools/capture_shots.sh 2752x2064 Stores/ios/screenshots/13-inch-ipad
#   bash tools/capture_shots.sh 2752x2064 Stores/ios/screenshots/13-inch-ipad mobile
#
# The third argument switches the touch interface on for the phone and tablet sets.
#
# App Store Connect rejects screenshots that carry an alpha channel, so every file is
# flattened to rgb24 and tagged with the sRGB profile.
set -euo pipefail
cd "$(dirname "$0")/.."

SIZE="${1:-2752x2064}"
OUT="${2:-Stores/ios/screenshots/13-inch-ipad}"
EXTRA=""
[ "${3:-}" = "mobile" ] && EXTRA="--shot-mobile"
WIDTH="${SIZE%x*}"
HEIGHT="${SIZE#*x}"
GAME=src/SuperSqij.DesktopGL/bin/Release/net10.0/SuperSqij.dll
SRGB="/System/Library/ColorSync/Profiles/sRGB Profile.icc"
FRAMES=45
WORK=build/capture/shots

[ -f "$GAME" ] || { echo "build first: dotnet build src/SuperSqij.DesktopGL -c Release" >&2; exit 1; }
mkdir -p "$OUT" "$WORK"

# The high-score screen shows the built-in sample table when no file exists.
STORE="$HOME/Library/Application Support/SuperSqij"
mkdir -p build/highscores-backup
for f in highscores.txt name.txt; do
  cp "$STORE/$f" "build/highscores-backup/$f" 2>/dev/null || true
done
rm -f "$STORE/highscores.txt"

restore() {
  for f in highscores.txt name.txt; do
    [ -f "build/highscores-backup/$f" ] && cp "build/highscores-backup/$f" "$STORE/$f" || true
  done
}
trap restore EXIT

shot() { # mode name slot
  local mode=$1 name=$2 slot=${3:-0}
  local raw="$WORK/$name.rgba"
  dotnet "$GAME" --capture "$raw" "$WIDTH" "$HEIGHT" "$FRAMES" "$slot" --shot "$mode" $EXTRA > "$WORK/$name.log" 2>&1
  # The last frame is the settled one: fades and room banners have finished.
  ffmpeg -y -v error -f rawvideo -pix_fmt rgba -s "${WIDTH}x${HEIGHT}" -r 30 -i "$raw" \
    -vf "select='eq(n\,$((FRAMES - 1)))'" -fps_mode passthrough -pix_fmt rgb24 \
    "$OUT/$name.png"
  sips --matchTo "$SRGB" "$OUT/$name.png" > /dev/null
  rm -f "$raw"
  echo "$name.png  $(sips -g pixelWidth -g pixelHeight -g hasAlpha "$OUT/$name.png" |
    awk '/pixelWidth|pixelHeight|hasAlpha/ {printf "%s ", $2}')"
}

shot play    02-gameplay 1
shot title   01-title
shot scores  03-high-scores
shot credits 04-credits

rm -rf "$WORK"
echo "wrote 4 screenshots at ${WIDTH}x${HEIGHT} to $OUT"
