"""Merges the C1/C2 proposal files (prompt propose-c1c2-v1) into one deduplicated list for build_inventory.py --extra.

Rules: drop lexemes already present among the CEFR-J (A1-B2) lemmas of the inventory chunks or proposed twice (first area wins), drop confidence < MIN_CONFIDENCE,
normalise POS to the app enum, keep glossEn and the proposed topics as hints for the generator.
"""
from __future__ import annotations

import json
import sys
import unicodedata
from collections import Counter
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
REPO = Path(__file__).resolve().parents[3]
INVENTORY = REPO / "content/generated/uk-en/inventory"
PROPOSALS = INVENTORY / "proposals"
MIN_CONFIDENCE = 0.5
POS_OK = {"noun", "verb", "adjective", "adverb", "pronoun", "preposition", "conjunction", "determiner", "interjection", "numeral", "phrase"}
TOPICS = {"communication", "travel", "work", "it", "food", "health", "city", "shopping", "military"}


def key(lemma: str) -> str:
    return unicodedata.normalize("NFC", lemma).strip().lower()


def main() -> None:
    known = {key(l["lemma"]) for chunk in INVENTORY.glob("??-???.json")
             for l in json.loads(chunk.read_text(encoding="utf-8"))["lexemes"] if l["levelSource"] == "cefrj-1.6"}
    merged, seen, stats = [], set(), Counter()
    for path in sorted(PROPOSALS.glob("*.json")):
        document = json.loads(path.read_text(encoding="utf-8"))
        for item in document.get("lexemes", []):
            lemma = (item.get("lemma") or "").strip()
            pos = (item.get("pos") or "").strip().lower()
            level = (item.get("level") or "").strip().upper()
            k = key(lemma)
            if not lemma or pos not in POS_OK or level not in ("C1", "C2"):
                stats["malformed"] += 1
                continue
            if k in known:
                stats["duplicate_a1_b2"] += 1
                continue
            if (k, pos) in seen:
                stats["duplicate_proposal"] += 1
                continue
            if float(item.get("confidence", 0)) < MIN_CONFIDENCE:
                stats["low_confidence"] += 1
                continue
            seen.add((k, pos))
            merged.append({
                "lemma": lemma, "pos": pos, "level": level, "confidence": float(item.get("confidence", 0)),
                "glossEn": (item.get("glossEn") or "").strip(),
                "topics": [t for t in item.get("topics", []) if t in TOPICS][:3],
                "area": document.get("area", path.stem),
            })
            stats[level] += 1
    out = INVENTORY / "c1c2-proposals.json"
    out.write_text(json.dumps(merged, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print(json.dumps({"merged": len(merged), **stats}, ensure_ascii=False))


if __name__ == "__main__":
    main()
