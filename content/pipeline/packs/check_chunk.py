"""Checks one generated chunk (and optionally its review) against the inventory with the same rules build_packs.py applies.

Usage: python content/pipeline/packs/check_chunk.py <chunk-id> [--review]
Exit code 1 when the chunk (or review) has hard errors, so agents can run it as their last step and fix what it reports.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_packs  # noqa: E402  (shared validate / contains_lemma rules)

REPO = HERE.parents[2]
GENERATED = REPO / "content/generated/uk-en"
REQUIRED = ("id", "lemma", "pos", "level", "acceptedForms", "translation", "translationStressed", "altTranslations", "register",
            "genderNote", "pluralNote", "aspectNote", "usageNote", "ipaGa", "ipaRp", "exampleEn", "exampleUk", "exampleEn2", "exampleUk2", "topics", "widgetSafe")
REASON_CODES = {"wrong_sense", "unnatural_uk", "russism", "wrong_gender", "wrong_aspect", "wrong_stress", "wrong_ipa", "wrong_topic", "too_hard",
                "too_long", "unsafe", "mismatch_en_uk", "apostrophe", "missing_lemma", "other"}
SCORES = ("accuracy", "naturalness", "levelFit", "safety", "grammarNotes", "topics")


def load(path: Path) -> dict | None:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except FileNotFoundError:
        print(f"MISSING {path}")
        return None
    except json.JSONDecodeError as ex:
        print(f"INVALID JSON {path.name}: {ex}")
        return None


def check_chunk(chunk_id: str) -> tuple[int, int]:
    inventory_doc = load(GENERATED / "inventory" / f"{chunk_id}.json")
    chunk = load(GENERATED / "chunks" / f"{chunk_id}.json")
    if inventory_doc is None or chunk is None:
        return 1, 0
    inventory = {lexeme["id"]: {**lexeme, "chunk": chunk_id} for lexeme in inventory_doc["lexemes"]}
    level = inventory_doc["level"]
    errors, warnings = 0, 0
    if chunk.get("chunk") != chunk_id or chunk.get("level") != level:
        print(f"ERROR header: chunk={chunk.get('chunk')} level={chunk.get('level')} expected {chunk_id}/{level}")
        errors += 1
    entries = chunk.get("entries", [])
    expected_ids = [lexeme["id"] for lexeme in inventory_doc["lexemes"]]
    got_ids = [entry.get("id") for entry in entries]
    if got_ids != expected_ids:
        missing = [i for i in expected_ids if i not in got_ids]
        extra = [i for i in got_ids if i not in expected_ids]
        print(f"ERROR entries: {len(entries)}/{len(expected_ids)}; missing {len(missing)}, unknown {len(extra)}, order_ok={set(got_ids) == set(expected_ids)}")
        for lexeme in inventory_doc["lexemes"]:
            if lexeme["id"] in missing:
                print(f"   missing: {lexeme['lemma']} ({lexeme['id']})")
        errors += 1
    for entry in entries:
        label = entry.get("lemma", entry.get("id"))
        absent = [key for key in REQUIRED if key not in entry]
        if absent:
            print(f"ERROR {label}: missing keys {absent}")
            errors += 1
        if not isinstance(entry.get("widgetSafe"), bool):
            print(f"ERROR {label}: widgetSafe must be true/false")
            errors += 1
        if not isinstance(entry.get("topics"), list) or any(not isinstance(t, dict) or "slug" not in t for t in entry.get("topics") or []):
            print(f"ERROR {label}: topics must be a list of {{slug, confidence}}")
            errors += 1
        for key in ("exampleUk", "exampleUk2", "translation", "translationStressed"):
            value = entry.get(key)
            if isinstance(value, str) and "'" in value:
                print(f"WARN {label}: ASCII apostrophe in {key}; use U+2019")
                warnings += 1
        hard, soft = build_packs.validate(entry, level, inventory)
        if entry.get("exampleEn2") and not build_packs.contains_lemma(entry["exampleEn2"], list(dict.fromkeys([entry.get("lemma", ""), *(entry.get("acceptedForms") or [])]))):
            soft = [*soft, "lemma_not_in_example2"]
        for code in hard:
            print(f"ERROR {label}: {code}")
        for code in soft:
            print(f"WARN {label}: {code}")
        errors += len(hard)
        warnings += len(soft)
    return errors, warnings


def check_review(chunk_id: str) -> tuple[int, int]:
    chunk = load(GENERATED / "chunks" / f"{chunk_id}.json")
    review = load(GENERATED / "reviews" / f"{chunk_id}.review.json")
    if chunk is None or review is None:
        return 1, 0
    errors, warnings = 0, 0
    if review.get("chunk") != chunk_id:
        print(f"ERROR review header: chunk={review.get('chunk')}")
        errors += 1
    expected_ids = [entry.get("id") for entry in chunk.get("entries", [])]
    got_ids = [item.get("id") for item in review.get("items", [])]
    if got_ids != expected_ids:
        print(f"ERROR review items: {len(got_ids)}/{len(expected_ids)} (order and ids must match the chunk)")
        errors += 1
    flagged = 0
    for item in review.get("items", []):
        scores = item.get("scores") or {}
        bad = [k for k in SCORES if not isinstance(scores.get(k), int) or not 1 <= scores[k] <= 5]
        if bad:
            print(f"ERROR review {item.get('id')}: scores {bad}")
            errors += 1
        for flag in ("autoReject", "needsRegeneration"):
            if not isinstance(item.get(flag), bool):
                print(f"ERROR review {item.get('id')}: {flag} must be true/false")
                errors += 1
        for issue in item.get("issues") or []:
            if issue.get("reasonCode") not in REASON_CODES:
                print(f"WARN review {item.get('id')}: unknown reasonCode {issue.get('reasonCode')}")
                warnings += 1
        if item.get("needsRegeneration") or item.get("autoReject"):
            flagged += 1
    print(f"review flagged {flagged} of {len(got_ids)}")
    return errors, warnings


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    chunk_id = sys.argv[1]
    errors, warnings = check_chunk(chunk_id)
    if "--review" in sys.argv:
        r_errors, r_warnings = check_review(chunk_id)
        errors += r_errors
        warnings += r_warnings
    print(json.dumps({"chunk": chunk_id, "errors": errors, "warnings": warnings}))
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
