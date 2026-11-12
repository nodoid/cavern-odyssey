#!/usr/bin/env bash
# Builds the store MSIX packages, with Microsoft's own packer when it is available.
#
#   bash tools/build_msix.sh              # both architectures
#   bash tools/build_msix.sh x64 arm64
#
# makemsix (microsoft/msix-packaging) is the cross-platform equivalent of makeappx.exe and
# writes the block map the Store validator expects. Run tools/build_makemsix.sh once to
# build it; without it this falls back to tools/make_msix.py, which now mirrors the same
# layout but cannot be validated by Microsoft's tooling here.
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="${VERSION:-1.0}"
ARCHES=("${@:-x64 arm64}")
[ -z "${1:-}" ] && ARCHES=(x64 arm64)

MAKEMSIX="${MAKEMSIX:-}"
if [ -z "$MAKEMSIX" ]; then
  if command -v makemsix > /dev/null; then
    MAKEMSIX=$(command -v makemsix)
  elif [ -x build/tools/makemsix ]; then
    MAKEMSIX=build/tools/makemsix
  fi
fi
[ -n "$MAKEMSIX" ] && echo "packer: $MAKEMSIX" || echo "packer: tools/make_msix.py (no makemsix found)"

mkdir -p releases build/staging

for arch in "${ARCHES[@]}"; do
  publish="build/win-$arch"
  [ -d "$publish" ] || { echo "missing publish dir $publish (dotnet publish -r win-$arch)" >&2; exit 1; }
  out="releases/CavernOdyssey-${VERSION}-win-${arch}.msix"
  stage="build/staging/msix-$arch"

  rm -rf "$stage"
  mkdir -p "$stage"
  (cd "$publish" && find . -type f ! -name '*.pdb' -print0 | cpio -0pdm "$OLDPWD/$stage" 2> /dev/null) \
    || cp -R "$publish"/. "$stage"/
  find "$stage" -name '*.pdb' -delete
  rm -rf "$stage/Assets"
  cp -R packaging/windows/Assets "$stage/Assets"
  sed "s/ProcessorArchitecture=\"[A-Za-z0-9]*\"/ProcessorArchitecture=\"$arch\"/" \
    packaging/windows/AppxManifest.xml > "$stage/AppxManifest.xml"
  # Store submissions need the publisher ID assigned in Partner Center here, which is not
  # the subject of the sideloading certificate. PUBLISHER="CN=..." overrides it.
  if [ -n "${PUBLISHER:-}" ]; then
    sed -i.bak "s|Publisher=\"[^\"]*\"|Publisher=\"$PUBLISHER\"|" "$stage/AppxManifest.xml"
    rm -f "$stage/AppxManifest.xml.bak"
  fi
  # DISPLAY_NAME overrides the reserved product name without editing the manifest.
  if [ -n "${DISPLAY_NAME:-}" ]; then
    sed -i.bak "s|<DisplayName>[^<]*</DisplayName>|<DisplayName>$DISPLAY_NAME</DisplayName>|" \
      "$stage/AppxManifest.xml"
    sed -i.bak "s|DisplayName=\"[^\"]*\"|DisplayName=\"$DISPLAY_NAME\"|" "$stage/AppxManifest.xml"
    rm -f "$stage/AppxManifest.xml.bak"
  fi

  rm -f "$out"
  if [ -n "$MAKEMSIX" ]; then
    log="build/msix-pack-$arch.log"
    if ! "$MAKEMSIX" pack -d "$stage" -p "$out" > "$log" 2>&1; then
      echo "packing $arch failed:" >&2
      tail -20 "$log" >&2
      exit 1
    fi
  else
    python3 tools/make_msix.py "$stage" "$arch" "$out"
  fi
  rm -rf "$stage"
  printf "%-46s %s\n" "$out" "$(du -h "$out" | cut -f1)"
done
