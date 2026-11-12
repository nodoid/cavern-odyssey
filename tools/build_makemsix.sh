#!/usr/bin/env bash
# Builds Microsoft's makemsix packer into build/tools/makemsix.
#
#   bash tools/build_makemsix.sh
#
# makemsix is the cross-platform build of the MSIX SDK (microsoft/msix-packaging) and is
# the only packer available off Windows that the Store accepts without question. The build
# takes several minutes: it compiles the bundled zlib and Xerces-C that pack mode needs.
set -euo pipefail
cd "$(dirname "$0")/.."

SRC=build/msix-packaging
BIN=build/tools/makemsix
LOG=build/makemsix-build.log
ARCH=$(uname -m) # arm64 or x86_64

if [ -x "$BIN" ]; then
  echo "$BIN already built"
  exit 0
fi

if [ ! -d "$SRC" ]; then
  git clone --depth 1 https://github.com/microsoft/msix-packaging.git "$SRC"
fi

echo "building makemsix for $ARCH (log: $LOG)"
(cd "$SRC" && ./makemac.sh --pack --skip-tests --skip-samples -arch "$ARCH" > "../../../$LOG" 2>&1) || {
  tail -20 "$LOG" >&2
  exit 1
}

mkdir -p build/tools
cp "$SRC/.vs/bin/makemsix" "$BIN"
echo "built: $BIN"
