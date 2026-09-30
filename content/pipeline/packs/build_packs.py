"""Validates generated chunks and assembles content packs (spec 08 §3.9 gates, §3.10 packs; stages lint + pack build).

Input:  content/generated/uk-en/chunks/<chunk>.json (generate-v1) and content/generated/uk-en/reviews/<chunk>.review.json (review-v1)
Output: content/generated/uk-en/packs/uk-en.<level>.<topic>.json, uk-en.<level>.general.json, uk-en.free-pool.json, index.json,
        content/generated/uk-en/packs/report.json (validation findings, needs-human list, counts)

Entries whose review still says autoReject (and that were not regenerated) are excluded from packs and listed for the human queue.
Ukrainian text is normalised (apostrophe → U+2019); hard rule violations exclude the entry and are reported.
"""
from __future__ import annotations

import datetime as dt
import hashlib
import json
import re
import sys
import unicodedata
from collections import Counter, defaultdict
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
REPO = Path(__file__).resolve().parents[3]
GEN = REPO / "content/generated/uk-en"
CHUNKS, REVIEWS, PACKS = GEN / "chunks", GEN / "reviews", GEN / "packs"
INVENTORY = GEN / "inventory"

TOPICS = ["communication", "travel", "work", "it", "food", "health", "city", "shopping", "military"]
POS = {"noun", "verb", "adjective", "adverb", "pronoun", "preposition", "conjunction", "determiner", "interjection", "numeral", "phrase"}
LEVELS = ["A1", "A2", "B1", "B2", "C1", "C2"]
MIN_TOPIC_CONFIDENCE = 0.6
MAX_WORDS = {"A1": 12, "A2": 12}
FREE_POOL_PER_LEVEL = {"A1": 200, "A2": 200, "B1": 200}
APOSTROPHES = str.maketrans({"'": "’", "ʼ": "’", "‘": "’", "´": "’"})
FORBIDDEN_UK = set("ыэъёЫЭЪЁ")
CYRILLIC_WORD = re.compile(r"[А-Яа-яЄєІіЇїҐґ’]+")
LATIN_IN_CYRILLIC = re.compile(r"(?=\w*[А-Яа-яЄєІіЇїҐґ])(?=\w*[A-Za-z])\w+")
STRESS = "́"


def uk_normalise(text: str | None) -> str | None:
    return None if text is None else unicodedata.normalize("NFC", text).translate(APOSTROPHES).strip()


def word_count(text: str) -> int:
    return len(re.findall(r"[A-Za-z0-9'’-]+", text))


def stem(form: str) -> str:
    form = form.lower()
    return form[: max(4, len(form) - 2)] if len(form) > 5 else form[: max(3, len(form) - 1)]


def contains_lemma(sentence: str, forms: list[str]) -> bool:
    low = sentence.lower()
    for form in forms:
        if form.lower() in low or (" " not in form and stem(form) in low):
            return True
    return False


def validate(entry: dict, level: str, inventory: dict) -> tuple[list[str], list[str]]:
    """Returns (hard errors → excluded, soft warnings → kept)."""
    errors, warnings = [], []
    ref = inventory.get(entry.get("id"))
    if ref is None:
        errors.append("unknown_id")
        return errors, warnings
    if entry.get("lemma") != ref["lemma"] or entry.get("pos") != ref["pos"] or entry.get("level") != level:
        errors.append("identity_mismatch")
    if entry.get("pos") not in POS:
        errors.append("bad_pos")
    for key in ("translation", "exampleEn", "exampleUk"):
        if not isinstance(entry.get(key), str) or not entry[key].strip():
            errors.append(f"missing_{key}")
    if errors:
        return errors, warnings
    for key in ("translation", "translationStressed", "exampleUk", "exampleUk2"):
        value = entry.get(key)
        if isinstance(value, str):
            if FORBIDDEN_UK & set(value):
                errors.append(f"forbidden_letter_{key}")
            if LATIN_IN_CYRILLIC.search(value.replace(STRESS, "")):
                errors.append(f"latin_in_cyrillic_{key}")
    for alt in entry.get("altTranslations") or []:
        if FORBIDDEN_UK & set(alt):
            errors.append("forbidden_letter_alt")
    forms = list(dict.fromkeys([entry["lemma"], *(entry.get("acceptedForms") or [])]))
    if not contains_lemma(entry["exampleEn"], forms):
        errors.append("lemma_not_in_example")
    limit = MAX_WORDS.get(level, 16)
    if word_count(entry["exampleEn"]) > limit + 2:
        errors.append("example_too_long")
    elif word_count(entry["exampleEn"]) > limit:
        warnings.append("example_slightly_long")
    for key in ("ipaGa", "ipaRp"):
        ipa = entry.get(key)
        if not isinstance(ipa, str) or not (ipa.startswith("/") and ipa.endswith("/")):
            warnings.append(f"ipa_format_{key}")
    if len(entry["translation"]) > 40:
        warnings.append("translation_long")
    return errors, warnings


def clean_topics(entry: dict) -> list[str]:
    out = []
    for topic in entry.get("topics") or []:
        slug, conf = topic.get("slug"), topic.get("confidence", 0)
        if slug in TOPICS and isinstance(conf, (int, float)) and conf >= MIN_TOPIC_CONFIDENCE and slug not in out:
            out.append(slug)
    return out[:3]


def to_pack_entry(entry: dict, topics: list[str]) -> dict:
    forms = []
    for form in [entry["lemma"], *(entry.get("acceptedForms") or [])]:
        f = form.strip()
        if f and f.lower() not in [x.lower() for x in forms]:
            forms.append(f)
    return {
        "id": entry["id"], "lemma": entry["lemma"], "pos": entry["pos"], "level": entry["level"], "topics": topics,
        "translation": uk_normalise(entry["translation"]),
        "altTranslations": [uk_normalise(a) for a in (entry.get("altTranslations") or []) if a and a.strip()][:3],
        "acceptedForms": forms,
        "ipaGa": entry.get("ipaGa") or None, "ipaRp": entry.get("ipaRp") or None,
        "exampleEn": entry["exampleEn"].strip(), "exampleUk": uk_normalise(entry["exampleUk"]),
        "frequencyBand": None,
    }


def sha256_text(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def write_pack(pack_id: str, level: str, topic: str, entries: list[dict], now: str, attributions: list[str], has_llm_levels: bool) -> dict:
    document = {
        "schemaVersion": 1, "packId": pack_id, "pair": "uk-en", "level": level, "topic": topic, "version": 1, "generatedAt": now,
        "provenance": {
            "source": "burdock-pipeline generate-v1 + review-v1 (Claude Code, claude-fable-5-1)",
            "status": "ai_reviewed", "promptVersion": "generate-v1", "reviewer": "review-v1",
            "attributions": attributions + (["llm-level-estimate"] if has_llm_levels else []),
        },
        "entries": entries,
    }
    head = json.dumps({k: v for k, v in document.items() if k != "entries"}, ensure_ascii=False, indent=1)
    rows = ",\n".join("  " + json.dumps(e, ensure_ascii=False, separators=(",", ":")) for e in entries)
    text = head[:-2] + ',\n "entries": [\n' + rows + "\n ]\n}\n"
    path = PACKS / f"{pack_id}.json"
    path.write_text(text, encoding="utf-8")
    return {"packId": pack_id, "file": path.name, "level": level, "topic": topic, "version": 1, "lexemeCount": len(entries), "sizeBytes": len(text.encode("utf-8")), "sha256": sha256_text(text)}


def pick_free_pool(by_level: dict[str, list[dict]]) -> list[dict]:
    chosen = []
    for level, target in FREE_POOL_PER_LEVEL.items():
        candidates = [e for e in by_level.get(level, [])
                      if e["_widgetSafe"] and e["pos"] in ("noun", "verb", "adjective") and len(e["lemma"]) <= 12 and len(e["translation"]) <= 16
                      and " " not in e["lemma"] and not e["_homograph"]]
        with_topic = [e for e in candidates if e["topics"]]
        without = [e for e in candidates if not e["topics"]]
        ordered = sorted(with_topic, key=lambda e: (len(e["lemma"]), e["lemma"])) + sorted(without, key=lambda e: (len(e["lemma"]), e["lemma"]))
        chosen.extend(ordered[:target])
    return chosen


def main() -> None:
    now = dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds")
    inventory = {}
    for path in INVENTORY.glob("*-*.json"):
        for lexeme in json.loads(path.read_text(encoding="utf-8"))["lexemes"]:
            inventory[lexeme["id"]] = lexeme
    reviews = {}
    for path in REVIEWS.glob("*.review.json"):
        for item in json.loads(path.read_text(encoding="utf-8")).get("items", []):
            reviews[item["id"]] = item

    PACKS.mkdir(parents=True, exist_ok=True)
    for old in PACKS.glob("*.json"):
        old.unlink()

    accepted, report = [], {"chunks": 0, "entriesSeen": 0, "excluded": Counter(), "warnings": Counter(), "needsHuman": [], "excludedIds": []}
    for path in sorted(CHUNKS.glob("*.json")):
        chunk = json.loads(path.read_text(encoding="utf-8"))
        level = chunk["level"]
        report["chunks"] += 1
        for entry in chunk["entries"]:
            report["entriesSeen"] += 1
            errors, warnings = validate(entry, level, inventory)
            review = reviews.get(entry.get("id"))
            if review and review.get("autoReject") and not entry.get("regenerated"):
                errors.append("review_auto_reject")
            if review and review.get("needsRegeneration") and not entry.get("regenerated"):
                report["needsHuman"].append({"id": entry.get("id"), "lemma": entry.get("lemma"), "chunk": chunk["chunk"], "issues": [i.get("reasonCode") for i in review.get("issues", [])]})
            for w in warnings:
                report["warnings"][w] += 1
            if errors:
                for e in errors:
                    report["excluded"][e] += 1
                report["excludedIds"].append({"id": entry.get("id"), "lemma": entry.get("lemma"), "chunk": chunk["chunk"], "errors": errors})
                continue
            pack_entry = to_pack_entry(entry, clean_topics(entry))
            pack_entry["_widgetSafe"] = bool(entry.get("widgetSafe", True))
            pack_entry["_levelSource"] = inventory[entry["id"]].get("levelSource", "cefrj-1.6")
            accepted.append(pack_entry)

    # homographs: same lemma with different POS in the same level (excluded from the free pool, spec §3.2)
    lemma_pos = defaultdict(set)
    for e in accepted:
        lemma_pos[(e["level"], e["lemma"].lower())].add(e["pos"])
    for e in accepted:
        e["_homograph"] = len(lemma_pos[(e["level"], e["lemma"].lower())]) > 1

    by_level = defaultdict(list)
    for e in accepted:
        by_level[e["level"]].append(e)

    index = {"schemaVersion": 1, "pair": "uk-en", "generatedAt": now, "packs": []}
    attributions = ["cefrj-1.6", "claude-outputs"]

    def strip(e: dict) -> dict:
        return {k: v for k, v in e.items() if not k.startswith("_")}

    for level in LEVELS:
        entries = sorted(by_level.get(level, []), key=lambda e: e["lemma"].lower())
        if not entries:
            continue
        llm = any(e["_levelSource"] == "llm_estimate" for e in entries)
        for topic in TOPICS:
            topical = [strip(e) for e in entries if topic in e["topics"]]
            if topical:
                index["packs"].append(write_pack(f"uk-en.{level.lower()}.{topic}", level, topic, topical, now, attributions, llm))
        general = [strip(e) for e in entries if not e["topics"]]
        if general:
            index["packs"].append(write_pack(f"uk-en.{level.lower()}.general", level, "general", general, now, attributions, llm))

    free = [strip(e) for e in pick_free_pool(by_level)]
    if free:
        index["packs"].append(write_pack("uk-en.free-pool", "A1-B1", "free-pool", free, now, attributions, False))

    (PACKS / "index.json").write_text(json.dumps(index, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    report["accepted"] = len(accepted)
    report["byLevel"] = {lv: len(v) for lv, v in by_level.items()}
    report["byTopic"] = dict(Counter(t for e in accepted for t in e["topics"]))
    report["general"] = sum(1 for e in accepted if not e["topics"])
    report["freePool"] = len(free)
    report["excluded"] = dict(report["excluded"])
    report["warnings"] = dict(report["warnings"])
    report["packs"] = len(index["packs"])
    (PACKS / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print(json.dumps({k: report[k] for k in ("chunks", "entriesSeen", "accepted", "byLevel", "general", "freePool", "packs", "excluded", "warnings")}, ensure_ascii=False))
    print(f"needs human: {len(report['needsHuman'])}; excluded: {len(report['excludedIds'])}")


if __name__ == "__main__":
    main()
