"""Build the local dictionary from pinned, SHA-256-verified upstream files.

Python standard library only. Personal vocabulary is never read by this script.
"""
import csv
import hashlib
import json
from pathlib import Path
import re
import shutil
import sqlite3
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "assets"
CACHE = ROOT / ".cache" / "ecdict"
SOURCES = json.loads((Path(__file__).with_name("dictionary-sources.json")).read_text(encoding="utf-8"))


def fetch(name):
    destination = CACHE / name
    expected = SOURCES["files"][name]["sha256"]
    if destination.exists() and hashlib.sha256(destination.read_bytes()).hexdigest() == expected:
        return destination
    url = f"https://raw.githubusercontent.com/skywind3000/ECDICT/{SOURCES['commit']}/{name}"
    print(f"Downloading {name}...", flush=True)
    request = urllib.request.Request(url, headers={"User-Agent": "Shici-dictionary-builder/0.1"})
    with urllib.request.urlopen(request, timeout=120) as response:
        data = response.read()
    if hashlib.sha256(data).hexdigest() != expected:
        raise ValueError(f"SHA-256 mismatch: {name}; existing dictionary was not changed")
    destination.write_bytes(data)
    return destination


def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    ASSETS.mkdir(parents=True, exist_ok=True)
    files = {name: fetch(name) for name in SOURCES["files"]}
    temporary = CACHE / "dictionary-building.db"
    # Only remove this builder's own temporary output, never a user's data directory.
    if temporary.exists():
        temporary.unlink()
    db = sqlite3.connect(temporary)
    db.executescript("""
        PRAGMA journal_mode=OFF;
        CREATE TABLE words(word TEXT PRIMARY KEY COLLATE NOCASE, phonetic TEXT, translation TEXT,
          definition TEXT, exchange TEXT, tags TEXT, rank INTEGER);
        CREATE TABLE forms(form TEXT, lemma TEXT, kind TEXT, rank INTEGER, PRIMARY KEY(form,lemma));
        CREATE TABLE roots(word TEXT, root TEXT, meaning TEXT, class TEXT, origin TEXT, examples TEXT);
    """)
    rows, forms = [], []
    with files["ecdict.csv"].open(encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            word = row["word"].strip().lower()
            if not word:
                continue
            rank = int(row.get("frq") or row.get("bnc") or 999999) or 999999
            rows.append((word, row["phonetic"], row["translation"].replace("\\n", "\n"),
                         row["definition"].replace("\\n", "\n"), row["exchange"], row["tag"], rank))
            for part in row["exchange"].split("/"):
                if ":" not in part:
                    continue
                kind, value = part.split(":", 1)
                if kind not in ("p", "d", "i", "3", "r", "t", "s"):
                    continue
                for form in value.split(","):
                    form = form.strip().lower()
                    if form and form != word:
                        forms.append((form, word, kind, rank))
    db.executemany("INSERT OR REPLACE INTO words VALUES (?,?,?,?,?,?,?)", rows)
    db.executemany("INSERT OR IGNORE INTO forms VALUES (?,?,?,?)", forms)
    roots = json.loads(files["wordroot.txt"].read_text(encoding="utf-8"))
    links = []
    for key, item in roots.items():
        for word in item.get("example", []):
            links.append((re.sub(r"\d+$", "", word).lower(), item.get("root", key),
                          item.get("meaning", ""), item.get("class", ""), item.get("origin", ""),
                          ", ".join(item.get("example", [])[:8])))
    db.executemany("INSERT INTO roots VALUES (?,?,?,?,?,?)", links)
    db.executescript("CREATE INDEX root_word ON roots(word); CREATE INDEX form_lookup ON forms(form,rank);")
    db.commit()
    counts = {"entries": db.execute("SELECT COUNT(*) FROM words").fetchone()[0],
              "inflections": db.execute("SELECT COUNT(*) FROM forms").fetchone()[0],
              "root_entries": len(roots), "word_root_links": len(links)}
    assert counts["entries"] == 770611, counts
    assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
    db.close()
    shutil.copyfile(temporary, ASSETS / "dictionary.db")
    shutil.copyfile(files["LICENSE"], ASSETS / "ECDICT-LICENSE.txt")
    print(json.dumps(counts), flush=True)


if __name__ == "__main__":
    main()
