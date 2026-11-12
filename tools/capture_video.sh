#!/usr/bin/env bash
# Builds a Cavern Odyssey gameplay video (video + audio) for one platform.
#
#   bash tools/capture_video.sh macos|ipad [output.mp4]
#
# Each clip is recorded by the game's --capture mode: raw frames plus, through
# OpenAL Soft's wave backend, the game's own mixed audio. The clips are trimmed to
# length, faded, and joined into one 30 second montage.
set -euo pipefail
cd "$(dirname "$0")/.."

PLATFORM="${1:-macos}"
case "$PLATFORM" in
  macos) WIDTH=1920; HEIGHT=1080; DEFAULT_OUT="$HOME/Movies/Cavern Odyssey - macOS gameplay.mp4" ;;
  ipad)  WIDTH=1600; HEIGHT=1200; DEFAULT_OUT="$HOME/Movies/Cavern Odyssey - iPad gameplay.mp4" ;;
  *) echo "usage: $0 macos|ipad [output.mp4]" >&2; exit 2 ;;
esac
OUT="${2:-$DEFAULT_OUT}"

GAME=src/SuperSqij.DesktopGL/bin/Release/net10.0/SuperSqij.dll
[ -f "$GAME" ] || { echo "build first: dotnet build src/SuperSqij.DesktopGL -c Release" >&2; exit 1; }

WORK="build/capture/$PLATFORM"
rm -rf "$WORK"
mkdir -p "$WORK"
FPS=30
# slot:seconds - slot 0 opens with the splash and title, the rest drop straight into a room.
CLIPS=("0:8" "1:7" "2:7" "3:8")

LIST="$WORK/clips.txt"
: > "$LIST"

i=0
for clip in "${CLIPS[@]}"; do
  SLOT="${clip%%:*}"
  SECONDS_LEN="${clip##*:}"
  FRAMES=$((SECONDS_LEN * FPS))
  RAW="$WORK/clip$i.rgba"
  WAV="$WORK/clip$i.wav"

  printf '[general]\ndrivers = wave\n[wave]\nfile = %s\n' "$WAV" > "$WORK/alsoft.conf"
  LOG="$WORK/clip$i.log"
  ALSOFT_CONF="$PWD/$WORK/alsoft.conf" \
    dotnet "$GAME" --capture "$RAW" "$WIDTH" "$HEIGHT" "$FRAMES" "$SLOT" > "$LOG" 2>&1

  OFFSET=$(sed -n 's/.*CAPTURE: audio offset \(.*\)s$/\1/p' "$LOG")
  [ -n "$OFFSET" ] || { echo "no audio offset in $LOG" >&2; cat "$LOG" >&2; exit 1; }
  echo "clip $i: slot $SLOT, ${SECONDS_LEN}s, audio starts at ${OFFSET}s"

  FADE_OUT=$(awk -v d="$SECONDS_LEN" 'BEGIN { printf "%.2f", d - 0.3 }')
  ffmpeg -y -v error \
    -f rawvideo -pix_fmt rgba -s "${WIDTH}x${HEIGHT}" -r "$FPS" -i "$RAW" \
    -ss "$OFFSET" -i "$WAV" \
    -map 0:v -map 1:a \
    -c:v libx264 -preset slow -crf 20 -pix_fmt yuv420p \
    -af "afade=in:st=0:d=0.12,afade=out:st=${FADE_OUT}:d=0.3" \
    -c:a aac -b:a 192k -ar 48000 -ac 2 \
    -t "$SECONDS_LEN" -movflags +faststart "$WORK/clip$i.mp4"
  printf "file '%s'\n" "$PWD/$WORK/clip$i.mp4" >> "$LIST"

  rm -f "$RAW" "$WAV"
  i=$((i + 1))
done

ffmpeg -y -v error -f concat -safe 0 -i "$LIST" -c copy -movflags +faststart "$OUT"
rm -rf "$WORK"
echo "built: $OUT"
ffprobe -v error -show_entries stream=codec_name,width,height,r_frame_rate \
  -show_entries format=duration -of default=nw=1 "$OUT"
