"""Builds the lexeme inventory for generation from the CEFR-J Wordlist v1.6 (spec 08 §3.2, stages ingest + normalize).

Reads content/sources/raw/cefrj/CEFR-J Wordlist Ver1.6.xlsx (not committed; see content/sources/README.md for the
licence row) and writes:
  content/generated/uk-en/inventory/<LEVEL>-<NNN>.json   chunks of CHUNK_SIZE lexemes: id, lemma, pos, level, levelSource, acceptedForms
  content/generated/uk-en/inventory/lemmas.txt          every lemma key, one per line (for the C1/C2 proposal stage to avoid duplicates)
  content/generated/uk-en/inventory/summary.json        counts per level and POS

Ids are UUID v5 of "uk-en|<lemma_key>|<pos>" so re-running the inventory never renumbers a lexeme.
Optional --extra <json> appends model-proposed C1/C2 lexemes ([{lemma, pos, level}]) with levelSource "llm_estimate".
"""
from __future__ import annotations

import argparse
import json
import re
import sys
import unicodedata
import uuid
from collections import Counter, OrderedDict
from pathlib import Path

import openpyxl

sys.stdout.reconfigure(encoding="utf-8")
REPO = Path(__file__).resolve().parents[3]
SOURCE = REPO / "content/sources/raw/cefrj/CEFR-J Wordlist Ver1.6.xlsx"
OUT = REPO / "content/generated/uk-en/inventory"
NAMESPACE = uuid.uuid5(uuid.NAMESPACE_URL, "https://burdock.org/lexeme")
CHUNK_SIZE = 40
LEVELS = ["A1", "A2", "B1", "B2", "C1", "C2"]
POS_MAP = {
    "noun": "noun", "adjective": "adjective", "verb": "verb", "adverb": "adverb", "pronoun": "pronoun",
    "preposition": "preposition", "determiner": "determiner", "conjunction": "conjunction", "number": "numeral",
    "modal auxiliary": "verb", "be-verb": "verb", "do-verb": "verb", "have-verb": "verb", "interjection": "interjection",
    "infinitive-to": "preposition", "phrase": "phrase",
}


def lemma_key(lemma: str) -> str:
    return unicodedata.normalize("NFC", lemma).strip().lower()


def lexeme_id(lemma: str, pos: str) -> str:
    return str(uuid.uuid5(NAMESPACE, f"uk-en|{lemma_key(lemma)}|{pos}"))


def read_cefrj() -> list[dict]:
    wb = openpyxl.load_workbook(SOURCE, read_only=True)
    rows = list(wb["ALL"].iter_rows(values_only=True))[1:]
    lexemes: OrderedDict[tuple[str, str], dict] = OrderedDict()
    for headword, pos_raw, level, *_ in rows:
        if not headword or not level:
            continue
        pos = POS_MAP.get((pos_raw or "").strip())
        if pos is None:
            raise SystemExit(f"unmapped POS {pos_raw!r} for {headword!r}")
        variants = [v.strip() for v in str(headword).split("/") if v.strip()]
        lemma = variants[0]
        if lemma.startswith("'") or lemma.startswith("’"):
            continue  # contractions ('m, 're, 's, n't) are grammar, not vocabulary to learn
        forms = []
        for v in variants:
            low = v if re.search(r"[A-Z]{2,}", v) else v.lower()
            if low not in forms:
                forms.append(low)
        key = (lemma_key(lemma), pos)
        entry = {"id": lexeme_id(lemma, pos), "lemma": lemma, "pos": pos, "level": level, "levelSource": "cefrj-1.6", "acceptedForms": forms}
        if key in lexemes:
            # duplicate headword+pos: keep the easier level, merge spelling variants
            existing = lexemes[key]
            if LEVELS.index(level) < LEVELS.index(existing["level"]):
                existing["level"] = level
            for f in forms:
                if f not in existing["acceptedForms"]:
                    existing["acceptedForms"].append(f)
        else:
            lexemes[key] = entry
    return list(lexemes.values())


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--extra", help="JSON list of model-proposed C1/C2 lexemes to append")
    args = parser.parse_args()

    lexemes = read_cefrj()
    known = {(lemma_key(l["lemma"]), l["pos"]) for l in lexemes}
    if args.extra:
        extra = json.loads(Path(args.extra).read_text(encoding="utf-8"))
        added = 0
        for item in extra:
            pos = POS_MAP.get(item["pos"], item["pos"])
            key = (lemma_key(item["lemma"]), pos)
            if key in known or item["level"] not in ("C1", "C2"):
                continue
            known.add(key)
            lexemes.append({"id": lexeme_id(item["lemma"], pos), "lemma": item["lemma"].strip(), "pos": pos, "level": item["level"],
                            "levelSource": "llm_estimate", "acceptedForms": [lemma_key(item["lemma"])],
                            "glossEn": item.get("glossEn") or None, "proposedTopics": item.get("topics") or [],
                            "levelConfidence": item.get("confidence")})
            added += 1
        print(f"extra: {added} C1/C2 lexemes appended")

    OUT.mkdir(parents=True, exist_ok=True)
    written: set[Path] = set()
    summary = {"levels": {}, "pos": Counter(l["pos"] for l in lexemes), "chunks": []}
    for level in LEVELS:
        items = [l for l in lexemes if l["level"] == level]
        items.sort(key=lambda l: (lemma_key(l["lemma"]), l["pos"]))
        summary["levels"][level] = len(items)
        for n, start in enumerate(range(0, len(items), CHUNK_SIZE), start=1):
            chunk = items[start:start + CHUNK_SIZE]
            name = f"{level}-{n:03d}"
            path = OUT / f"{name}.json"
            path.write_text(json.dumps({"chunk": name, "level": level, "lexemes": chunk}, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
            written.add(path)
            summary["chunks"].append({"chunk": name, "level": level, "count": len(chunk)})
    # chunk files are overwritten in place (generation agents may be reading them); only stale chunk files go away,
    # other files in the folder (c1c2-proposals.json, proposals/) are left alone
    for stale in OUT.glob("??-???.json"):
        if stale not in written:
            stale.unlink()
    (OUT / "lemmas.txt").write_text("\n".join(sorted({lemma_key(l["lemma"]) for l in lexemes})) + "\n", encoding="utf-8")
    summary["pos"] = dict(summary["pos"].most_common())
    summary["total"] = len(lexemes)
    (OUT / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print(json.dumps({"total": summary["total"], "levels": summary["levels"], "chunks": len(summary["chunks"])}, ensure_ascii=False))


if __name__ == "__main__":
    main()
