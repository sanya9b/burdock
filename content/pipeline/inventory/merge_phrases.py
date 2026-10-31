"""Merges the fixed-phrase proposal files (prompt propose-phrases-v1) into inventory chunks for generate-v1.

Input:  content/generated/uk-en/inventory/phrases/<kind>-<NN>.json
Output: content/generated/uk-en/inventory/phrases-proposals.json (merged, deduplicated list with stats)
        content/generated/uk-en/inventory/<LEVEL>-P<NN>.json       chunks of CHUNK_SIZE phrases (id, lemma, pos=phrase, level,
                                                                   levelSource=llm_estimate, acceptedForms, kind, glossEn, topics)
        content/generated/uk-en/inventory/lemmas.txt                 appended with the new lemmas (one per line)

Rules: pos is always "phrase"; drop entries whose lemma already exists in lemmas.txt or appeared earlier (first file wins,
files sorted by name); drop confidence < MIN_CONFIDENCE; keep only known kinds, levels and topics. Chunk ids follow the
`??-???` pattern of build_packs.py and carry the level, so packs are assembled per level like the word chunks.
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
PHRASES = INVENTORY / "phrases"
LEMMAS = INVENTORY / "lemmas.txt"
NAMESPACE = uuid.uuid5(uuid.NAMESPACE_URL, "https://burdock.org/lexeme")
CHUNK_SIZE = 40
MIN_CONFIDENCE = 0.5
KINDS = {"phrasal_verb", "idiom", "collocation", "formula", "prepositional", "binomial"}
LEVELS = ["A1", "A2", "B1", "B2", "C1", "C2"]
TOPICS = {"communication", "travel", "work", "it", "food", "health", "city", "shopping", "military"}


def lemma_key(lemma: str) -> str:
    return " ".join(unicodedata.normalize("NFC", lemma).strip().lower().replace("’", "'").split())


def lexeme_id(lemma: str) -> str:
    return str(uuid.uuid5(NAMESPACE, f"uk-en|{lemma_key(lemma)}|phrase"))


def main() -> None:
    write = "--write" in sys.argv
    known = {lemma_key(line) for line in LEMMAS.read_text(encoding="utf-8").splitlines() if line.strip()} if LEMMAS.exists() else set()
    merged: list[dict] = []
    seen: set[str] = set()
    stats: Counter = Counter()
    for path in sorted(PHRASES.glob("*.json")):
        try:
            document = json.loads(path.read_text(encoding="utf-8-sig"))
        except json.JSONDecodeError as ex:
            print(f"SKIP {path.name}: {ex}")
            stats["unparseable_files"] += 1
            continue
        for item in document.get("lexemes", []):
            lemma = " ".join((item.get("lemma") or "").split())
            kind = (item.get("kind") or document.get("kind") or "").strip()
            level = (item.get("level") or "").strip().upper()
            k = lemma_key(lemma)
            if not lemma or kind not in KINDS or level not in LEVELS:
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
                "id": lexeme_id(lemma), "lemma": lemma, "pos": "phrase", "level": level, "levelSource": "llm_estimate",
                "acceptedForms": forms[:5], "kind": kind, "confidence": float(item.get("confidence", 0)),
                "glossEn": (item.get("glossEn") or "").strip(),
                "topics": [t for t in item.get("topics", []) if t in TOPICS][:3],
                "source": path.stem,
            })
            stats[f"kind:{kind}"] += 1
            stats[f"level:{level}"] += 1

    summary = {"merged": len(merged), **dict(sorted(stats.items()))}
    print(json.dumps(summary, ensure_ascii=False, indent=1))
    if not write:
        print("dry run; pass --write to produce chunks, phrases-proposals.json and extend lemmas.txt")
        return

    (INVENTORY / "phrases-proposals.json").write_text(json.dumps({"summary": summary, "lexemes": merged}, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    for old in INVENTORY.glob("??-P??.json"):
        old.unlink()
    chunks = 0
    for level in LEVELS:
        items = [m for m in merged if m["level"] == level]
        for n, start in enumerate(range(0, len(items), CHUNK_SIZE), start=1):
            chunk_id = f"{level}-P{n:02d}"
            lexemes = [{key: value for key, value in item.items() if key not in ("confidence", "source")} for item in items[start:start + CHUNK_SIZE]]
            (INVENTORY / f"{chunk_id}.json").write_text(json.dumps({"chunk": chunk_id, "level": level, "lexemes": lexemes}, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
            chunks += 1
    with LEMMAS.open("a", encoding="utf-8", newline="\n") as handle:
        for item in merged:
            handle.write(item["lemma"] + "\n")
    print(f"wrote {chunks} chunks, appended {len(merged)} lemmas to lemmas.txt")


if __name__ == "__main__":
    main()
