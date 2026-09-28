"""Generates FSRS golden vectors from py-fsrs (spec 15 section 3.4, FR-15-2, FR-06-3).

Usage:
    python -m venv .venv && .venv/Scripts/pip install -r requirements.txt
    .venv/Scripts/python generate_vectors.py > vectors.v1.json

The vectors are the source of truth for every IReviewScheduler implementation (Fsrs.Sharp adapter today, a port tomorrow).
Never edit vectors.v1.json by hand; bump the py-fsrs pin in requirements.txt and regenerate instead.
Scheduler tunables follow spec 06 section 3.3: one learning step of 10 minutes, one relearning step of 10 minutes,
maximum interval 365 days, fuzz disabled (fuzz is tested separately as an invariant).
"""
from __future__ import annotations

import json
import random
import sys
from datetime import datetime, timedelta, timezone

from fsrs import Card, Rating, Scheduler, State

PY_FSRS_VERSION = "6.3.2"
SEED = 20260928
START = datetime(2026, 1, 5, 9, 0, 0, tzinfo=timezone.utc)
LEARNING_STEPS = (timedelta(minutes=10),)
RELEARNING_STEPS = (timedelta(minutes=10),)
MAX_INTERVAL_DAYS = 365
DEFAULT_RETENTION = 0.90
RETENTION_PRESETS = (0.85, 0.90, 0.94)
DAY = 86400.0


def make_scheduler(retention: float) -> Scheduler:
    return Scheduler(
        desired_retention=retention,
        learning_steps=LEARNING_STEPS,
        relearning_steps=RELEARNING_STEPS,
        maximum_interval=MAX_INTERVAL_DAYS,
        enable_fuzzing=False,
    )


def run_case(case_id: str, reviews: list[tuple[float, int]], retention: float = DEFAULT_RETENTION) -> dict:
    """reviews: (deltaDays since the previous review, grade 1-4). The first delta is measured from START."""
    scheduler = make_scheduler(retention)
    card = Card()
    now = START
    expected = []
    for delta_days, grade in reviews:
        now = now + timedelta(seconds=round(delta_days * DAY))
        retrievability_before = scheduler.get_card_retrievability(card, now) if card.last_review is not None else None
        card, _log = scheduler.review_card(card, Rating(grade), now)
        # Ten decimals keep the file small; the comparison tolerance is 1e-6 (stability, difficulty) and one second (due).
        expected.append({
            "stability": round(card.stability, 10),
            "difficulty": round(card.difficulty, 10),
            "dueDays": round((card.due - now).total_seconds() / DAY, 10),
            "state": int(card.state),
            "step": card.step,
            "retrievabilityBefore": None if retrievability_before is None else round(retrievability_before, 10),
        })
    case = {
        "id": case_id,
        "reviews": [{"deltaDays": d, "grade": g} for d, g in reviews],
        "expected": expected,
    }
    if retention != DEFAULT_RETENTION:
        case["desiredRetention"] = retention
    return case


def random_sequence(rng: random.Random, length: int) -> list[tuple[float, int]]:
    """A plausible learner: mostly Good, sometimes Hard/Easy/Again; reviews land around the due date, sometimes early or late."""
    scheduler = make_scheduler(DEFAULT_RETENTION)
    card = Card()
    now = START
    reviews: list[tuple[float, int]] = []
    for index in range(length):
        if index == 0:
            delta_days = 0.0
        else:
            scheduled_days = max((card.due - now).total_seconds() / DAY, 1 / 144)  # never below the 10-minute step
            factor = rng.choice((0.5, 0.9, 1.0, 1.0, 1.0, 1.2, 1.8, 3.0))
            delta_days = scheduled_days * factor
            if card.state == State.Review:
                delta_days = max(1.0, round(delta_days))  # review-state reviews on whole days, like a real learner
            else:
                delta_days = round(delta_days, 6)
        grade = rng.choices((1, 2, 3, 4), weights=(12, 18, 55, 15))[0]
        now = now + timedelta(seconds=round(delta_days * DAY))
        card, _ = scheduler.review_card(card, Rating(grade), now)
        reviews.append((delta_days, grade))
    return reviews


def main() -> None:
    rng = random.Random(SEED)
    cases: list[dict] = []

    # (a) new card x 4 grades
    for grade in (1, 2, 3, 4):
        cases.append(run_case(f"a-new-{Rating(grade).name.lower()}", [(0.0, grade)]))

    # (b) 150 random sequences of 30 reviews
    for i in range(150):
        cases.append(run_case(f"b-random-{i:03d}", random_sequence(rng, 30)))

    # (c) retention presets 0.85 / 0.90 / 0.94 on the same 20 sequences
    preset_sequences = [random_sequence(rng, 20) for _ in range(20)]
    for retention in RETENTION_PRESETS:
        for i, seq in enumerate(preset_sequences):
            cases.append(run_case(f"c-retention-{retention:.2f}-{i:02d}", seq, retention))

    # (d) maximum interval clamp: Easy on every due date until the interval would exceed 365 days
    easy_sequence: list[tuple[float, int]] = [(0.0, 4)]
    scheduler = make_scheduler(DEFAULT_RETENTION)
    card = Card()
    now = START
    card, _ = scheduler.review_card(card, Rating.Easy, now)
    for _ in range(14):
        delta = max(1.0, round((card.due - now).total_seconds() / DAY))
        now = now + timedelta(days=delta)
        card, _ = scheduler.review_card(card, Rating.Easy, now)
        easy_sequence.append((delta, 4))
    cases.append(run_case("d-max-interval-clamp", easy_sequence))

    # (e) relearning after Again
    cases.append(run_case("e-relearning", [(0.0, 3), (1 / 144, 3), (1.0, 3), (3.0, 3), (7.0, 1), (1 / 144, 3), (1.0, 3), (2.0, 4)]))
    cases.append(run_case("e-relearning-again-twice", [(0.0, 3), (1 / 144, 3), (2.0, 3), (5.0, 1), (1 / 144, 1), (1 / 144, 3), (1.0, 3)]))

    # (f) same-day repeats never push the due date beyond one day
    cases.append(run_case("f-same-day", [(0.0, 3), (1 / 1440, 3), (5 / 1440, 3), (30 / 1440, 3), (2 / 24, 3), (6 / 24, 3)]))
    cases.append(run_case("f-same-day-hard", [(0.0, 2), (1 / 144, 2), (1 / 144, 2), (1 / 144, 3), (1 / 144, 3)]))

    document = {
        "$schema": "burdock-fsrs-golden-vectors/v1",
        "generator": {"library": "py-fsrs", "version": PY_FSRS_VERSION, "script": "tests/fixtures/fsrs/generate_vectors.py", "seed": SEED},
        "parameters": list(make_scheduler(DEFAULT_RETENTION).parameters),
        "desiredRetention": DEFAULT_RETENTION,
        "maxIntervalDays": MAX_INTERVAL_DAYS,
        "learningStepsMinutes": [int(s.total_seconds() // 60) for s in LEARNING_STEPS],
        "relearningStepsMinutes": [int(s.total_seconds() // 60) for s in RELEARNING_STEPS],
        "start": START.isoformat(),
        "states": {"learning": int(State.Learning), "review": int(State.Review), "relearning": int(State.Relearning)},
        "cases": cases,
    }
    # Header pretty-printed, one case per line: reviewable diffs without a multi-megabyte file.
    head = json.dumps({k: v for k, v in document.items() if k != "cases"}, indent=1, ensure_ascii=False)
    assert head.endswith("\n}")
    rows = ",\n".join("  " + json.dumps(case, separators=(",", ":"), ensure_ascii=False) for case in cases)
    sys.stdout.write(head[:-2] + ',\n "cases": [\n' + rows + "\n ]\n}\n")


if __name__ == "__main__":
    main()
