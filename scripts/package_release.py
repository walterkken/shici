"""Package a clean Windows release; never include personal data or debug paths."""
import hashlib
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile

root = Path(__file__).resolve().parents[1]
source = root / "dist" / "Shici"
version = ET.parse(root / "source" / "Shici.csproj").findtext(".//Version")
archive = root / "dist" / f"Shici-v{version}-win-x64.zip"
blocked_dirs = {"data", "exports", ".selftests", "bin", "obj"}
blocked_suffixes = {".pdb", ".lnk", ".log", ".bak", ".tmp"}
required = ["Shici.exe", "Shici.dll", "Shici.deps.json", "Shici.runtimeconfig.json", "assets/dictionary.db"]
for name in required:
    if not (source / name).is_file():
        raise FileNotFoundError(name)
with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as output:
    for path in sorted(source.rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(source)
        if any(part in blocked_dirs for part in relative.parts) or path.suffix.lower() in blocked_suffixes:
            continue
        if path.name.endswith(("-test.json", "-diagnostic.json", "-error.txt")) or path.name == "verification.json":
            continue
        output.write(path, "Shici/" + relative.as_posix())
with zipfile.ZipFile(archive) as check:
    assert check.testzip() is None
    assert all("Shici/" + name in check.namelist() for name in required)
digest = hashlib.sha256(archive.read_bytes()).hexdigest()
(archive.parent / "SHA256SUMS.txt").write_text(f"{digest}  {archive.name}\n", encoding="ascii")
print(f"{archive.name}: {archive.stat().st_size:,} bytes; SHA-256 {digest}")
