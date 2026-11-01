"""Merges the word proposals of the 2026-11 stage (prompts propose-c1c2-v1 and propose-topical-v1) into inventory chunks.

Input:  content/generated/uk-en/inventory/proposals-2026-11/<area>-<NN>.json
Output: content/generated/uk-en/inventory/words-2026-11-proposals.json  (merged list with stats)
        content/generated/uk-en/inventory/<LEVEL>-W<NN>.json             chunks of CHUNK_SIZE lexemes for generate-v1
        content/generated/uk-en/inventory/lemmas.txt                     appended with the new lemmas

Rules: known POS only (no phrases: those have their own inventory), levels A1..C2, drop lemmas already in lemmas.txt or
proposed earlier (files sorted by name, first wins), drop confidence < MIN_CONFIDENCE, topics from the app list. Chunk ids
follow the `??-???` pattern of build_packs.py and carry the level; ids are the same UUID v5 as build_inventory.py.
"""
from __future__ import annotations

import json
import sys
import unicodedata
import uuid
from collections import Counter
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
REPO = Path(__file__).resolve().parents[3]
INVENTORY = REPO / "content/generated/uk-en/inventory"
PROPOSALS = INVENTORY / "proposals-2026-11"
LEMMAS = INVENTORY / "lemmas.txt"
NAMESPACE = uuid.uuid5(uuid.NAMESPACE_URL, "https://burdock.org/lexeme")
CHUNK_SIZE = 40
MIN_CONFIDENCE = 0.5
CHUNK_PREFIX = "W"
POS_OK = {"noun", "verb", "adjective", "adverb", "pronoun", "preposition", "conjunction", "determiner", "interjection", "numeral"}
POS_ALIASES = {"adj": "adjective", "adv": "adverb", "n": "noun", "v": "verb", "prep": "preposition", "conj": "conjunction", "det": "determiner", "interj": "interjection", "num": "numeral"}
LEVELS = ["A1", "A2", "B1", "B2", "C1", "C2"]
TOPICS = {"communication", "travel", "work", "it", "food", "health", "city", "shopping", "military"}


def lemma_key(lemma: str) -> str:
    return " ".join(unicodedata.normalize("NFC", lemma).strip().lower().replace("’", "'").split())


def lexeme_id(lemma: str, pos: str) -> str:
    return str(uuid.uuid5(NAMESPACE, f"uk-en|{lemma_key(lemma)}|{pos}"))


def main() -> None:
    write = "--write" in sys.argv
    known = {lemma_key(line) for line in LEMMAS.read_text(encoding="utf-8").splitlines() if line.strip()} if LEMMAS.exists() else set()
    merged: list[dict] = []
    seen: set[str] = set()
    stats: Counter = Counter()
    for path in sorted(PROPOSALS.glob("*.json")):
        try:
            document = json.loads(path.read_text(encoding="utf-8-sig"))
        except json.JSONDecodeError as ex:
            print(f"SKIP {path.name}: {ex}")
            stats["unparseable_files"] += 1
            continue
        area = document.get("area", path.stem)
        for item in document.get("lexemes", []):
            lemma = " ".join((item.get("lemma") or "").split())
            pos = (item.get("pos") or "").strip().lower()
            pos = POS_ALIASES.get(pos, pos)
            level = (item.get("level") or "").strip().upper()
            k = lemma_key(lemma)
            if not lemma or pos not in POS_OK or level not in LEVELS:
                stats["malformed"] += 1
                continue
            if k in known:
                stats["duplicate_existing"] += 1
                continue
            if k in seen:
                stats["duplicate_proposal"] += 1
                continue
            if float(item.get("confidence", 0)) < MIN_CONFIDENCE:
                stats["low_confidence"] += 1
                continue
            seen.add(k)
            forms = []
            for form in [lemma, *item.get("acceptedForms", [])]:
                form = " ".join(str(form).split())
                if form and form not in forms:
                    forms.append(form)
            merged.append({
                "id": lexeme_id(lemma, pos), "lemma": lemma, "pos": pos, "level": level, "levelSource": "llm_estimate",
                "acceptedForms": forms[:5], "confidence": float(item.get("confidence", 0)),
                "glossEn": (item.get("glossEn") or "").strip(),
                "topics": [t for t in item.get("topics", []) if t in TOPICS][:3],
                "area": area, "source": path.stem,
            })
            stats[f"area:{area}"] += 1
            stats[f"level:{level}"] += 1

    summary = {"merged": len(merged), **dict(sorted(stats.items()))}
    print(json.dumps(summary, ensure_ascii=False, indent=1))
    if not write:
        print("dry run; pass --write to produce chunks, words-2026-11-proposals.json and extend lemmas.txt")
        return

    (INVENTORY / "words-2026-11-proposals.json").write_text(json.dumps({"summary": summary, "lexemes": merged}, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    for old in INVENTORY.glob(f"??-{CHUNK_PREFIX}??.json"):
        old.unlink()
    chunks = 0
    for level in LEVELS:
        items = [m for m in merged if m["level"] == level]
        for n, start in enumerate(range(0, len(items), CHUNK_SIZE), start=1):
            chunk_id = f"{level}-{CHUNK_PREFIX}{n:02d}"
            lexemes = [{key: value for key, value in item.items() if key not in ("confidence", "source")} for item in items[start:start + CHUNK_SIZE]]
            (INVENTORY / f"{chunk_id}.json").write_text(json.dumps({"chunk": chunk_id, "level": level, "lexemes": lexemes}, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
            chunks += 1
    with LEMMAS.open("a", encoding="utf-8", newline="\n") as handle:
        for item in merged:
            handle.write(item["lemma"] + "\n")
    print(f"wrote {chunks} chunks, appended {len(merged)} lemmas to lemmas.txt")


if __name__ == "__main__":
    main()
