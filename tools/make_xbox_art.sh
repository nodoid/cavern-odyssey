#!/usr/bin/env bash
# Renders the XBOX art assets required by a Microsoft Store game listing.
#
#   bash tools/make_xbox_art.sh
#
# Sizes follow the Game Publishing Guide (preferred dimensions). Every asset is flat
# PNG without transparency: Super Hero art and Featured Promotional Square Art carry the
# artwork alone, while Poster art, Box art and Titled Hero art carry the title lockup in
# the upper two-thirds, clear of the lower-third text overlay.
set -euo pipefail
cd "$(dirname "$0")/.."

OUT=Stores/windows/xbox
WORK=build/capture/xbox
GAME=src/SuperSqij.DesktopGL/bin/Release/net10.0/SuperSqij.dll
LOGO=src/SuperSqij.Shared/Assets/logo.png
SRGB="/System/Library/ColorSync/Profiles/sRGB Profile.icc"
W=3840
H=2160
FRAMES=40

[ -f "$GAME" ] || { echo "build first: dotnet build src/SuperSqij.DesktopGL -c Release" >&2; exit 1; }
mkdir -p "$OUT" "$WORK"

# A room with a monsta, fruit and enemies, drawn without HUD, banner or key prompts.
dotnet "$GAME" --capture "$WORK/art.rgba" "$W" "$H" "$FRAMES" 1 --shot art 2>&1 | grep CAPTURE
ffmpeg -y -v error -f rawvideo -pix_fmt rgba -s "${W}x${H}" -r 30 -i "$WORK/art.rgba" \
  -vf "select='eq(n\,$((FRAMES - 1)))'" -fps_mode passthrough -frames:v 1 \
  -pix_fmt rgb24 "$WORK/master.png"

finish() { # file
  sips --matchTo "$SRGB" "$1" > /dev/null
  printf "%-42s %s\n" "$(basename "$1")" \
    "$(sips -g pixelWidth -g pixelHeight -g hasAlpha "$1" | awk '/pixelWidth|pixelHeight|hasAlpha/ {printf "%s ", $2}')"
}

# 1. Super Hero Art - 16:9 artwork, no title. Top of the listing and after trailers.
ffmpeg -y -v error -i "$WORK/master.png" -frames:v 1 -pix_fmt rgb24 \
  "$OUT/super-hero-art-3840x2160.png"
finish "$OUT/super-hero-art-3840x2160.png"

# 2. Featured Promotional Square Art - 1:1 artwork, no title.
ffmpeg -y -v error -i "$WORK/master.png" -frames:v 1 \
  -vf "crop=2160:2160:840:0,scale=1080:1080:flags=lanczos,format=rgb24" \
  "$OUT/featured-promotional-square-art-1080x1080.png"
finish "$OUT/featured-promotional-square-art-1080x1080.png"

# Titled art: a feathered shadow band behind the lockup keeps the wordmark legible over
# the room without a hard banner edge, and the title stays in the upper two-thirds.
titled() { # size  in-filter  logo-width  logo-y  out
  local cw=$1 ch=$2 crop=$3 lw=$4 ly=$5 out=$6
  local lh=$((lw * 340 / 1024))
  local top=$((ly - 90)) bottom=$((ly + lh + 90))
  ffmpeg -y -v error -i "$WORK/master.png" -i "$LOGO" \
    -filter_complex \
"[0:v]$crop,eq=brightness=-0.07:contrast=1.04[bg];\
[1:v]scale=$lw:-1:flags=neighbor[lg];\
color=black:s=${cw}x${ch},format=rgba,geq=r=0:g=0:b=0:a='185*clip((Y-$top)/150\,0\,1)*clip(($bottom-Y)/150\,0\,1)'[band];\
[bg][band]overlay=0:0[bgd];\
[bgd][lg]overlay=(W-w)/2:$ly:format=auto,format=rgb24[out]" \
    -map "[out]" -frames:v 1 "$out"
}

titled 1920 1080 "scale=1920:1080:flags=lanczos" 900 92 "$OUT/titled-hero-art-1920x1080.png"
finish "$OUT/titled-hero-art-1920x1080.png"

titled 1440 2160 "crop=1440:2160:1350:0" 1100 320 "$OUT/poster-art-1440x2160.png"
finish "$OUT/poster-art-1440x2160.png"

titled 2160 2160 "crop=2160:2160:840:0" 1400 260 "$OUT/box-art-2160x2160.png"
finish "$OUT/box-art-2160x2160.png"

rm -rf "$WORK"
echo "wrote 5 XBOX art assets to $OUT"
