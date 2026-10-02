"""Copies the clips referenced by an audio-index.json into the folder the app bundles (content/seed/audio → MauiAsset content/audio/**).

Usage:
  python content/pipeline/audio/bundle_audio.py --index content/generated/uk-en/audio/audio-index.json --out content/seed/audio

The index is produced by render_audio.py for the pack whose words must ship inside the app (v1: the free pool of 600 words,
ADR 0019 §2a: ≈ 3 MB per voice). Every referenced file is verified against the SHA-256 and size recorded in the index, the
destination voice folders are replaced atomically per voice, and the index is written next to the clips unchanged except
for `renderedAt`, which is kept, and `bundledAt`, which is added.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import shutil
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1 << 16), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--index", required=True, help="audio-index.json written by render_audio.py")
    parser.add_argument("--out", required=True, help="bundle folder (content/seed/audio)")
    args = parser.parse_args()

    index_path = Path(args.index)
    source = index_path.parent
    index = json.loads(index_path.read_text(encoding="utf-8"))
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    total_files = 0
    total_bytes = 0
    for voice in index["voices"]:
        files = {**voice["files"], "__sample__": voice["sample"]}
        staging = out / (voice["id"] + ".staging")
        if staging.exists():
            shutil.rmtree(staging)
        staging.mkdir()
        for key, meta in files.items():
            src = source / meta["path"]
            if not src.exists():
                print(f"missing clip {meta['path']} ({key})", file=sys.stderr)
                return 2
            if src.stat().st_size != meta["bytes"] or sha256(src) != meta["sha256"]:
                print(f"clip {meta['path']} does not match the index (size or sha256)", file=sys.stderr)
                return 3
            shutil.copy2(src, staging / Path(meta["path"]).name)
            total_files += 1
            total_bytes += meta["bytes"]
        target = out / voice["id"]
        if target.exists():
            shutil.rmtree(target)
        staging.rename(target)
        print(f"{voice['id']}: {len(voice['files'])} words + sample, {sum(f['bytes'] for f in voice['files'].values()) / 1024:.0f} KB")

    # voice folders that are no longer part of the selection are removed so the bundle never carries stale clips
    for stale in [d for d in out.iterdir() if d.is_dir() and d.name not in {v["id"] for v in index["voices"]}]:
        shutil.rmtree(stale)
        print(f"removed stale voice folder {stale.name}")

    index["bundledAt"] = dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds")
    (out / "audio-index.json").write_text(json.dumps(index, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"bundled {total_files} files, {total_bytes / 1024 / 1024:.1f} MB, pack {index['packId']} → {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
