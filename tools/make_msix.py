#!/usr/bin/env python3
"""Builds an unsigned MSIX package from a published Windows build.

makeappx.exe is a Windows-only tool, so this script writes the OPC container
itself: AppxBlockMap.xml, AppxManifest.xml and [Content_Types].xml plus the
payload. Everything is stored uncompressed, so each 64 KB block is a plain slice
of the file and needs no second compressor.

The layout follows what Microsoft's own packer writes, which is what the Store's
block map validator accepts:

* the block map lists every payload file plus AppxManifest.xml, and nothing else:
  AppxBlockMap.xml and [Content_Types].xml are not parts of it;
* ZIP entries keep forward slashes but block map names use backslashes, which is
  how makeappx writes them (LfhSize still describes the ZIP entry);
* Block elements carry only a Hash. The Size attribute exists for compressed
  blocks, where it holds the compressed length, so stored blocks must omit it;
* a file that is an exact multiple of 64 KB has no trailing empty block, and an
  empty file has no Block elements at all.

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


def win_name(name: str) -> str:
    """Packages name their parts with backslashes, as makeappx does."""
    return name.replace("/", "\\")


def local_header_size(name: str) -> int:
    # 30 byte fixed header plus the file name; no extra field is written.
    return 30 + len(name.encode("utf-8"))


def block_map(entries: list[tuple[str, bytes]]) -> bytes:
    parts = [
        '<?xml version="1.0" encoding="utf-8"?>',
        '<BlockMap xmlns="http://schemas.microsoft.com/appx/2010/blockmap" '
        'HashMethod="http://www.w3.org/2001/04/xmlenc#sha256">',
    ]
    for name, data in entries:
        parts.append(
            f'  <File Name="{win_name(name)}" Size="{len(data)}" '
            f'LfhSize="{local_header_size(name)}">'
        )
        for offset in range(0, len(data), BLOCK):
            digest = base64.b64encode(hashlib.sha256(data[offset:offset + BLOCK]).digest()).decode()
            parts.append(f'    <Block Hash="{digest}"/>')
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
    payload = list(sorted(collect(publish).items()))
    manifest = ("AppxManifest.xml", manifest_xml(arch))

    used = {name.rsplit(".", 1)[-1].lower() for name, _ in payload}
    used.add("xml")
    defaults = "\n".join(
        f'  <Default Extension="{ext}" ContentType="{EXTENSIONS.get(ext, "application/octet-stream")}"/>'
        for ext in sorted(used)
    )
    content_types = ("[Content_Types].xml", CONTENT_TYPES.format(defaults=defaults).encode("utf-8"))
    blockmap = ("AppxBlockMap.xml", block_map(payload + [manifest]))

    out.parent.mkdir(parents=True, exist_ok=True)
    if out.exists():
        out.unlink()

    # Same order as Microsoft's packer: payload, manifest, block map, content types.
    with zipfile.ZipFile(out, "w", zipfile.ZIP_STORED, allowZip64=True) as zf:
        for name, data in payload:
            zf.writestr(name, data)
        for name, data in (manifest, blockmap, content_types):
            zf.writestr(name, data)

    verify(out)
    size = out.stat().st_size / (1024 * 1024)
    print(f"built: {out} ({size:.1f} MB, {len(payload) + 3} entries)")


def verify(package: Path) -> None:
    """Re-reads the finished package and checks the rules the Store enforces."""
    import re

    zf = zipfile.ZipFile(package)
    infos = zf.infolist()
    names = [i.filename for i in infos]
    assert names.count("[Content_Types].xml") == 1 and names.count("AppxManifest.xml") == 1, names[:4]
    assert names[-3:] == ["AppxManifest.xml", "AppxBlockMap.xml", "[Content_Types].xml"], names[-3:]

    xml = zf.read("AppxBlockMap.xml").decode("utf-8")
    listed = re.findall(r'<File Name="([^"]+)" Size="(\d+)" LfhSize="(\d+)">(.*?)</File>', xml, re.S)
    # Block map names use backslashes; the matching ZIP entry uses slashes.
    assert all("/" not in name for name, _, _, _ in listed), "block map names must use backslashes"
    expected = set(names) - {"AppxBlockMap.xml", "[Content_Types].xml"}
    assert {name.replace("\\", "/") for name, _, _, _ in listed} == expected, \
        "block map must list the payload and the manifest only"

    offsets = {}
    for info in infos:
        offsets[info.filename] = info.header_offset
    for name, size, lfh, body in listed:
        data = zf.read(name.replace("\\", "/"))
        assert len(data) == int(size), f"{name}: Size is not the uncompressed length"
        assert "Size=\"" not in body, f"{name}: stored blocks must not carry a Size"
        blocks = re.findall(r'<Block Hash="([^"]+)"/>', body)
        chunks = [data[i:i + BLOCK] for i in range(0, len(data), BLOCK)]
        assert len(blocks) == len(chunks), f"{name}: {len(blocks)} blocks for {len(chunks)} chunks"
        for digest, chunk in zip(blocks, chunks):
            assert digest == base64.b64encode(hashlib.sha256(chunk).digest()).decode(), \
                f"{name}: block hash mismatch"
        # The local header length has to match the bytes actually in the ZIP.
        raw = package.read_bytes()
        zip_name = name.replace("\\", "/")
        nlen, elen = struct.unpack("<HH", raw[offsets[zip_name] + 26:offsets[zip_name] + 30])
        assert int(lfh) == 30 + nlen + elen, f"{name}: LfhSize {lfh} is wrong"
    print(f"verified: {len(listed)} block map entries, {len(names)} parts, no compressed Block sizes")


if __name__ == "__main__":
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    build(Path(sys.argv[1]), sys.argv[2], Path(sys.argv[3]))
