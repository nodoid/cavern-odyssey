#!/usr/bin/env python3
"""Builds an unsigned MSIX package from a published Windows build.

makeappx.exe is a Windows-only tool, so this script writes the OPC container
itself: [Content_Types].xml, AppxBlockMap.xml and AppxManifest.xml plus the
payload, all stored uncompressed so every block's compressed size is known
without a second compressor.

Usage: make_msix.py <publish-dir> <arch> <out.msix>
"""
import base64
import hashlib
import os
import shutil
import struct
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / "packaging" / "windows" / "AppxManifest.xml"
ASSETS = ROOT / "packaging" / "windows" / "Assets"
BLOCK = 64 * 1024

CONTENT_TYPES = """<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
{defaults}
  <Override PartName="/AppxManifest.xml" ContentType="application/vnd.ms-appx.manifest+xml"/>
  <Override PartName="/AppxBlockMap.xml" ContentType="application/vnd.ms-appx.blockmap+xml"/>
</Types>
"""

EXTENSIONS = {
    "dll": "application/octet-stream",
    "exe": "application/octet-stream",
    "png": "image/png",
    "ico": "image/vnd.microsoft.icon",
    "json": "application/json",
    "xml": "application/vnd.ms-appx.manifest+xml",
    "pri": "application/octet-stream",
    "dat": "application/octet-stream",
    "pdb": "application/octet-stream",
}


def manifest_xml(arch: str) -> bytes:
    text = MANIFEST.read_text(encoding="utf-8")
    text = text.replace('ProcessorArchitecture="x64"', f'ProcessorArchitecture="{arch}"')
    return text.encode("utf-8")


def local_header_size(name: str) -> int:
    # 30 byte fixed header plus the file name; no extra field is written.
    return 30 + len(name.encode("utf-8"))


def block_map(files: dict[str, bytes]) -> bytes:
    parts = [
        '<?xml version="1.0" encoding="utf-8"?>',
        '<BlockMap xmlns="http://schemas.microsoft.com/appx/2010/blockmap" '
        'HashMethod="http://www.w3.org/2001/04/xmlenc#sha256">',
    ]
    for name in sorted(files):
        data = files[name]
        parts.append(
            f'  <File Name="{name}" Size="{len(data)}" LfhSize="{local_header_size(name)}">'
        )
        for offset in range(0, max(len(data), 1), BLOCK):
            chunk = data[offset:offset + BLOCK]
            digest = base64.b64encode(hashlib.sha256(chunk).digest()).decode()
            # Stored entries: compressed size equals the block length.
            parts.append(f'    <Block Hash="{digest}" Size="{len(chunk)}"/>')
        parts.append("  </File>")
    parts.append("</BlockMap>")
    return "\n".join(parts).encode("utf-8")


def collect(publish: Path) -> dict[str, bytes]:
    files: dict[str, bytes] = {}
    for path in sorted(publish.rglob("*")):
        if not path.is_file():
            continue
        rel = path.relative_to(publish).as_posix()
        if rel.lower().endswith(".pdb"):
            continue
        files[rel] = path.read_bytes()

    for asset in sorted(ASSETS.iterdir()):
        if asset.is_file():
            files[f"Assets/{asset.name}"] = asset.read_bytes()
    return files


def build(publish: Path, arch: str, out: Path) -> None:
    files = collect(publish)
    files["AppxManifest.xml"] = manifest_xml(arch)

    used = {name.rsplit(".", 1)[-1].lower() for name in files}
    defaults = "\n".join(
        f'  <Default Extension="{ext}" ContentType="{EXTENSIONS.get(ext, "application/octet-stream")}"/>'
        for ext in sorted(used)
    )
    files["[Content_Types].xml"] = CONTENT_TYPES.format(defaults=defaults).encode("utf-8")
    files["AppxBlockMap.xml"] = block_map(files)

    out.parent.mkdir(parents=True, exist_ok=True)
    if out.exists():
        out.unlink()

    with zipfile.ZipFile(out, "w", zipfile.ZIP_STORED, allowZip64=True) as zf:
        for name in ["[Content_Types].xml", "AppxBlockMap.xml", "AppxManifest.xml"]:
            zf.writestr(name, files.pop(name))
        for name in sorted(files):
            zf.writestr(name, files[name])

    size = out.stat().st_size / (1024 * 1024)
    print(f"built: {out} ({size:.1f} MB, {len(files) + 3} entries)")


if __name__ == "__main__":
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    build(Path(sys.argv[1]), sys.argv[2], Path(sys.argv[3]))
