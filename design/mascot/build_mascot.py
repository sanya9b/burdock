#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Реп'яшок: generates the mascot SVGs from the guide in docs/specs/02-design-system.md section 3.10.

Usage:
  python design/mascot/build_mascot.py          writes design/mascot/src/mascot_<state>.svg and copies them
                                                 into src/mobile/Burdock.Presentation/Resources/Images/ (MauiImage)

Geometry: body circle 1u, 18 slightly irregular hooks of 0.10-0.14u with rounded tips, eyes 0.22x0.28u at 45 % height,
pupils 0.6 of the eye height with one highlight, brows carry the emotion. Colours are the primitive ramps named in the spec.
"""
from __future__ import annotations

import math
import random
import shutil
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
SRC = HERE / "src"
IMAGES = REPO / "src" / "mobile" / "Burdock.Presentation" / "Resources" / "Images"

BODY = "#9A8EFE"        # violet.400
BODY_LIGHT = "#B8B4FE"  # violet.300
BODY_SHADE = "#6D49EB"  # violet.600
HOOK = "#B4F461"        # lime.400
HOOK_TIP = "#689C00"    # lime.600
EYE = "#FFFFFF"         # neutral.0
PUPIL = "#15131F"       # neutral.950
BLUSH = "#FFCBC7"       # coral.200
GOLD = "#FFC53D"        # reward.gold (party confetti)
CORAL = "#FF6B6B"
SKY = "#4CC9F0"

SIZE = 256.0
CX, CY = 128.0, 136.0
R = 84.0  # body radius (1u = 2R)


def hooks(seed: int, raised: int | None = None, droop: float = 0.0) -> str:
    """18 rounded hooks around the body. `raised` lifts one hook like a waving hand; `droop` lowers the side hooks."""
    rnd = random.Random(seed)
    out = []
    n = 18
    for i in range(n):
        angle = -90 + i * 360 / n + rnd.uniform(-3, 3)
        length = R * rnd.uniform(0.20, 0.28)
        width = 13 + rnd.uniform(-1.5, 1.5)
        if raised is not None and i == raised:
            length = R * 0.42
            width = 15
        rad = math.radians(angle)
        if droop and abs(math.cos(rad)) > 0.6:
            angle += droop * (1 if math.cos(rad) > 0 else -1)
        base = R - 4
        x0, y0 = CX + math.cos(math.radians(angle)) * base, CY + math.sin(math.radians(angle)) * base
        # hook: capsule along the radial direction plus a darker rounded tip
        out.append(
            f'<rect x="{x0 - width / 2:.1f}" y="{y0 - length:.1f}" width="{width:.1f}" height="{length + 6:.1f}" rx="{width / 2:.1f}" '
            f'fill="{HOOK}" transform="rotate({angle + 90:.1f} {x0:.1f} {y0:.1f})"/>'
        )
        tip_r = width * 0.36
        tx = x0 + math.cos(math.radians(angle)) * (length - tip_r)
        ty = y0 + math.sin(math.radians(angle)) * (length - tip_r)
        out.append(f'<circle cx="{tx:.1f}" cy="{ty:.1f}" r="{tip_r:.1f}" fill="{HOOK_TIP}"/>')
    return "\n".join(out)


def body() -> str:
    return (
        f'<circle cx="{CX}" cy="{CY}" r="{R}" fill="url(#bodyGrad)"/>'
        f'<ellipse cx="{CX - 22}" cy="{CY - 38}" rx="30" ry="18" fill="{BODY_LIGHT}" opacity="0.55"/>'
    )


def eye(cx: float, cy: float, look: tuple[float, float] = (0, 0), scale: float = 1.0) -> str:
    rx, ry = 18 * scale, 23 * scale
    pr = ry * 0.6
    px, py = cx + look[0], cy + look[1]
    return (
        f'<ellipse cx="{cx}" cy="{cy}" rx="{rx:.1f}" ry="{ry:.1f}" fill="{EYE}"/>'
        f'<circle cx="{px:.1f}" cy="{py:.1f}" r="{pr:.1f}" fill="{PUPIL}"/>'
        f'<circle cx="{px - pr * 0.35:.1f}" cy="{py - pr * 0.4:.1f}" r="{pr * 0.28:.1f}" fill="{EYE}"/>'
    )


def eye_arc(cx: float, cy: float, up: bool = True) -> str:
    """Squinted happy eye (arc) for joy/proud; `up=False` gives a calm closed eye."""
    d = -1 if up else 1
    return (f'<path d="M {cx - 18} {cy + 4 * d} Q {cx} {cy - 18 * d} {cx + 18} {cy + 4 * d}" '
            f'fill="none" stroke="{PUPIL}" stroke-width="7" stroke-linecap="round"/>')


def brow(cx: float, cy: float, tilt: float = 0.0, lift: float = 0.0) -> str:
    y = cy - 34 - lift
    return (f'<path d="M {cx - 14} {y + 2 + tilt} Q {cx} {y - 6} {cx + 14} {y + 2 - tilt}" '
            f'fill="none" stroke="{BODY_SHADE}" stroke-width="5" stroke-linecap="round"/>')


def mouth(kind: str) -> str:
    y = CY + 34
    if kind == "smile":
        return f'<path d="M {CX - 14} {y} Q {CX} {y + 14} {CX + 14} {y}" fill="none" stroke="{PUPIL}" stroke-width="5" stroke-linecap="round"/>'
    if kind == "grin":
        return f'<path d="M {CX - 20} {y - 2} Q {CX} {y + 24} {CX + 20} {y - 2} Z" fill="{PUPIL}"/><path d="M {CX - 12} {y + 6} Q {CX} {y + 16} {CX + 12} {y + 6} Z" fill="{CORAL}"/>'
    if kind == "o":
        return f'<ellipse cx="{CX}" cy="{y + 4}" rx="7" ry="9" fill="{PUPIL}"/>'
    if kind == "dot":
        return f'<circle cx="{CX}" cy="{y + 2}" r="3.5" fill="{PUPIL}"/>'
    if kind == "flat":
        return f'<path d="M {CX - 10} {y + 2} L {CX + 10} {y + 2}" fill="none" stroke="{PUPIL}" stroke-width="5" stroke-linecap="round"/>'
    return ""


def blush() -> str:
    return (f'<ellipse cx="{CX - 46}" cy="{CY + 16}" rx="12" ry="7" fill="{BLUSH}" opacity="0.9"/>'
            f'<ellipse cx="{CX + 46}" cy="{CY + 16}" rx="12" ry="7" fill="{BLUSH}" opacity="0.9"/>')


def zees() -> str:
    """Two sleepy "z" glyphs drawn as polylines (no <text>, which the build-time SVG rasteriser rejects)."""
    def z(x: float, y: float, size: float) -> str:
        return (f'<path d="M {x} {y} L {x + size} {y} L {x} {y + size} L {x + size} {y + size}" fill="none" '
                f'stroke="{BODY_SHADE}" stroke-width="{size * 0.28:.1f}" stroke-linecap="round" stroke-linejoin="round"/>')
    return z(CX + 72, CY - 104, 18) + z(CX + 98, CY - 126, 12)


EYE_L, EYE_R, EYE_Y = CX - 24, CX + 24, CY - 8


def state(name: str) -> str:
    extra_before = ""
    extra_after = ""
    if name == "joy":
        face = eye_arc(EYE_L, EYE_Y) + eye_arc(EYE_R, EYE_Y) + brow(EYE_L, EYE_Y, lift=4) + brow(EYE_R, EYE_Y, lift=4) + blush() + mouth("smile")
        hk = hooks(7)
    elif name == "cheer":
        face = eye(EYE_L, EYE_Y, (2, -2)) + eye(EYE_R, EYE_Y, (2, -2)) + brow(EYE_L, EYE_Y, lift=6, tilt=-2) + brow(EYE_R, EYE_Y, lift=8, tilt=2) + mouth("smile")
        hk = hooks(11, raised=15)
    elif name == "surprise":
        face = eye(EYE_L, EYE_Y, scale=1.15) + eye(EYE_R, EYE_Y, scale=1.15) + brow(EYE_L, EYE_Y, lift=12) + brow(EYE_R, EYE_Y, lift=12) + mouth("o")
        hk = hooks(3)
    elif name == "sad":
        face = eye(EYE_L, EYE_Y, (-4, 3)) + eye(EYE_R, EYE_Y, (-4, 3)) + brow(EYE_L, EYE_Y, tilt=-5) + brow(EYE_R, EYE_Y, tilt=5) + mouth("flat")
        hk = hooks(5, droop=6)
    elif name == "sleep":
        face = eye_arc(EYE_L, EYE_Y, up=False) + eye_arc(EYE_R, EYE_Y, up=False) + mouth("dot")
        hk = hooks(9)
        # "z" letters as strokes: the MAUI resizetizer (Svg.Skia) cannot lay out <text>.
        extra_after = zees()
    elif name == "party":
        face = eye_arc(EYE_L, EYE_Y) + eye_arc(EYE_R, EYE_Y) + brow(EYE_L, EYE_Y, lift=6) + brow(EYE_R, EYE_Y, lift=6) + blush() + mouth("grin")
        hk = hooks(13)
        rnd = random.Random(21)
        bits = []
        for _ in range(14):
            x, y = rnd.uniform(12, SIZE - 12), rnd.uniform(8, 96)
            c = rnd.choice([GOLD, CORAL, SKY, HOOK])
            bits.append(f'<rect x="{x:.0f}" y="{y:.0f}" width="9" height="5" rx="1.5" fill="{c}" transform="rotate({rnd.uniform(-60, 60):.0f} {x:.0f} {y:.0f})"/>')
        extra_before = "\n".join(bits)
        extra_after = (f'<path d="M {CX - 26} {CY - R + 10} L {CX} {CY - R - 44} L {CX + 26} {CY - R + 10} Z" fill="{GOLD}"/>'
                       f'<circle cx="{CX}" cy="{CY - R - 44}" r="7" fill="{CORAL}"/>')
    elif name == "think":
        face = eye(EYE_L, EYE_Y, (5, -6)) + eye(EYE_R, EYE_Y, (5, -6)) + brow(EYE_L, EYE_Y, lift=2) + brow(EYE_R, EYE_Y, lift=10, tilt=3) + mouth("dot")
        hk = hooks(17)
        extra_after = "".join(f'<circle cx="{CX + 86 + i * 14}" cy="{CY - 92 - i * 12}" r="{4 + i}" fill="{BODY_SHADE}"/>' for i in range(3))
    elif name == "proud":
        face = eye_arc(EYE_L, EYE_Y, up=False) + eye_arc(EYE_R, EYE_Y, up=False) + brow(EYE_L, EYE_Y, lift=6) + brow(EYE_R, EYE_Y, lift=6) + blush() + mouth("smile")
        hk = hooks(19)
    else:
        raise ValueError(name)

    return f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {SIZE:.0f} {SIZE:.0f}" width="{SIZE:.0f}" height="{SIZE:.0f}">
<defs>
  <radialGradient id="bodyGrad" cx="0.38" cy="0.32" r="0.8">
    <stop offset="0" stop-color="{BODY_LIGHT}"/>
    <stop offset="0.55" stop-color="{BODY}"/>
    <stop offset="1" stop-color="{BODY_SHADE}"/>
  </radialGradient>
</defs>
{extra_before}
{hk}
{body()}
{face}
{extra_after}
</svg>
"""


def main() -> int:
    SRC.mkdir(parents=True, exist_ok=True)
    IMAGES.mkdir(parents=True, exist_ok=True)
    for name in ("joy", "cheer", "surprise", "sad", "sleep", "party", "think", "proud"):
        svg = state(name)
        target = SRC / f"mascot_{name}.svg"
        target.write_text(svg, encoding="utf-8", newline="\n")
        shutil.copyfile(target, IMAGES / target.name)
        print("wrote", target.relative_to(REPO).as_posix())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
