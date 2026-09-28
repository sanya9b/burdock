#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Brand assets derived from the mascot guide (docs/specs/02-design-system.md sections 3.2 and 3.10):

  python design/mascot/build_brand_assets.py

writes
  src/mobile/Burdock.Presentation/Resources/AppIcon/appicon.svg      icon background (violet.600 → violet.800)
  src/mobile/Burdock.Presentation/Resources/AppIcon/appiconfg.svg    icon foreground: the burr with two big eyes
  src/mobile/Burdock.Presentation/Resources/Splash/splash.svg        splash mark (same burr, transparent background)
  src/mobile/Burdock.Presentation/Resources/Images/brand_mark.svg    the same mark as a MauiImage for the in-app bootstrap page
  src/mobile/Burdock.Presentation/Platforms/Android/Resources/drawable/burdock_splash_icon.xml       VectorDrawable of the mark
  src/mobile/Burdock.Presentation/Platforms/Android/Resources/drawable-v31/burdock_splash_anim.xml   AnimatedVectorDrawable for the
                                                                                                     Android 12+ system splash

The icon has no brows or mouth so that it stays readable at 29 px; the geometry is the mascot's (1u circle, 16 hooks, eyes at 45 %).
"""
from __future__ import annotations

import math
import random
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
PRESENTATION = REPO / "src" / "mobile" / "Burdock.Presentation"

BODY, BODY_LIGHT, BODY_SHADE = "#9A8EFE", "#B8B4FE", "#6D49EB"   # violet.400 / 300 / 600
HOOK, HOOK_TIP = "#B4F461", "#689C00"                             # lime.400 / 600
EYE, PUPIL = "#FFFFFF", "#15131F"                                 # neutral.0 / 950
GRADIENT_START, GRADIENT_END = "#6D49EB", "#3E2C87"               # violet.600 / 800

SIZE = 1024.0
CX = CY = SIZE / 2
R = 300.0          # body radius in the 1024 canvas (the whole mark fits a 780 circle: safe for adaptive icons)
HOOKS = 16


def hook_geometry(seed: int = 5):
    rnd = random.Random(seed)
    for i in range(HOOKS):
        angle = -90 + i * 360 / HOOKS + rnd.uniform(-2, 2)
        length = R * rnd.uniform(0.22, 0.28)
        width = R * 0.17
        yield angle, length, width


def svg_hooks() -> str:
    out = []
    for angle, length, width in hook_geometry():
        base = R - 12
        x0, y0 = CX + math.cos(math.radians(angle)) * base, CY + math.sin(math.radians(angle)) * base
        out.append(f'<rect x="{x0 - width / 2:.1f}" y="{y0 - length:.1f}" width="{width:.1f}" height="{length + 20:.1f}" rx="{width / 2:.1f}" fill="{HOOK}" transform="rotate({angle + 90:.1f} {x0:.1f} {y0:.1f})"/>')
        tip_r = width * 0.34
        tx = x0 + math.cos(math.radians(angle)) * (length - tip_r)
        ty = y0 + math.sin(math.radians(angle)) * (length - tip_r)
        out.append(f'<circle cx="{tx:.1f}" cy="{ty:.1f}" r="{tip_r:.1f}" fill="{HOOK_TIP}"/>')
    return "\n".join(out)


def svg_face() -> str:
    ex, ey = 88, CY - 30
    parts = []
    for cx in (CX - ex, CX + ex):
        parts.append(f'<ellipse cx="{cx:.0f}" cy="{ey:.0f}" rx="66" ry="84" fill="{EYE}"/>')
        parts.append(f'<circle cx="{cx + 6:.0f}" cy="{ey + 8:.0f}" r="48" fill="{PUPIL}"/>')
        parts.append(f'<circle cx="{cx - 12:.0f}" cy="{ey - 14:.0f}" r="16" fill="{EYE}"/>')
    return "\n".join(parts)


MARK_RADIUS = R * 1.28 + 20  # body + longest hook + rounded end: the tight bounding circle of the mark


def mark_svg(with_background: bool, tight: bool = False) -> str:
    """The mark; `tight` crops the viewBox to the mark itself so that 1 rendered unit = the visible mark (in-app sizing)."""
    background = f'<rect width="{SIZE:.0f}" height="{SIZE:.0f}" fill="url(#bg)"/>' if with_background else ""
    if tight:
        side = 2 * MARK_RADIUS
        view_box = f"{CX - MARK_RADIUS:.0f} {CY - MARK_RADIUS:.0f} {side:.0f} {side:.0f}"
        width = height = f"{side:.0f}"
    else:
        view_box = f"0 0 {SIZE:.0f} {SIZE:.0f}"
        width = height = f"{SIZE:.0f}"
    return f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="{view_box}" width="{width}" height="{height}">
<defs>
  <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0" stop-color="{GRADIENT_START}"/>
    <stop offset="1" stop-color="{GRADIENT_END}"/>
  </linearGradient>
  <radialGradient id="bodyGrad" cx="0.38" cy="0.32" r="0.8">
    <stop offset="0" stop-color="{BODY_LIGHT}"/>
    <stop offset="0.55" stop-color="{BODY}"/>
    <stop offset="1" stop-color="{BODY_SHADE}"/>
  </radialGradient>
</defs>
{background}
{svg_hooks()}
<circle cx="{CX:.0f}" cy="{CY:.0f}" r="{R:.0f}" fill="url(#bodyGrad)"/>
<ellipse cx="{CX - 80:.0f}" cy="{CY - 140:.0f}" rx="110" ry="64" fill="{BODY_LIGHT}" opacity="0.5"/>
{svg_face()}
</svg>
"""


def background_svg() -> str:
    return f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {SIZE:.0f} {SIZE:.0f}" width="{SIZE:.0f}" height="{SIZE:.0f}">
<defs><linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="{GRADIENT_START}"/><stop offset="1" stop-color="{GRADIENT_END}"/></linearGradient></defs>
<rect width="{SIZE:.0f}" height="{SIZE:.0f}" fill="url(#bg)"/>
</svg>
"""


# ----------------------------------------------------------------------------- Android vector / animated vector

def p_circle(cx: float, cy: float, r: float) -> str:
    return f"M{cx - r:.1f},{cy:.1f} a{r:.1f},{r:.1f} 0 1,0 {2 * r:.1f},0 a{r:.1f},{r:.1f} 0 1,0 {-2 * r:.1f},0 Z"


def p_ellipse(cx: float, cy: float, rx: float, ry: float) -> str:
    return f"M{cx - rx:.1f},{cy:.1f} a{rx:.1f},{ry:.1f} 0 1,0 {2 * rx:.1f},0 a{rx:.1f},{ry:.1f} 0 1,0 {-2 * rx:.1f},0 Z"


def p_round_rect(x: float, y: float, w: float, h: float, r: float) -> str:
    r = min(r, w / 2, h / 2)
    return (f"M{x + r:.1f},{y:.1f} h{w - 2 * r:.1f} a{r:.1f},{r:.1f} 0 0 1 {r:.1f},{r:.1f} v{h - 2 * r:.1f} a{r:.1f},{r:.1f} 0 0 1 {-r:.1f},{r:.1f} "
            f"h{-(w - 2 * r):.1f} a{r:.1f},{r:.1f} 0 0 1 {-r:.1f},{-r:.1f} v{-(h - 2 * r):.1f} a{r:.1f},{r:.1f} 0 0 1 {r:.1f},{-r:.1f} Z")


def vector_drawable() -> str:
    """The mark in a 1024 viewport, scaled to 62 % so the Android 12 splash mask (inner two thirds) never clips a hook."""
    hooks = []
    for angle, length, width in hook_geometry():
        base = R - 12
        x0, y0 = CX + math.cos(math.radians(angle)) * base, CY + math.sin(math.radians(angle)) * base
        tip_r = width * 0.34
        tx = x0 + math.cos(math.radians(angle)) * (length - tip_r)
        ty = y0 + math.sin(math.radians(angle)) * (length - tip_r)
        hooks.append(
            f'    <group android:rotation="{angle + 90:.1f}" android:pivotX="{x0:.1f}" android:pivotY="{y0:.1f}">\n'
            f'      <path android:fillColor="{HOOK}" android:pathData="{p_round_rect(x0 - width / 2, y0 - length, width, length + 20, width / 2)}"/>\n'
            f'    </group>\n'
            f'    <path android:fillColor="{HOOK_TIP}" android:pathData="{p_circle(tx, ty, tip_r)}"/>'
        )
    ex, ey = 88, CY - 30
    eyes = []
    for cx in (CX - ex, CX + ex):
        eyes.append(f'      <path android:fillColor="{EYE}" android:pathData="{p_ellipse(cx, ey, 66, 84)}"/>')
        eyes.append(f'      <path android:fillColor="{PUPIL}" android:pathData="{p_circle(cx + 6, ey + 8, 48)}"/>')
        eyes.append(f'      <path android:fillColor="{EYE}" android:pathData="{p_circle(cx - 12, ey - 14, 16)}"/>')
    return f"""<?xml version="1.0" encoding="utf-8"?>
<!-- GENERATED by design/mascot/build_brand_assets.py from the mascot guide (spec 02 section 3.10). Do not edit by hand. -->
<vector xmlns:android="http://schemas.android.com/apk/res/android"
    xmlns:aapt="http://schemas.android.com/aapt"
    android:width="108dp" android:height="108dp"
    android:viewportWidth="1024" android:viewportHeight="1024">
  <group android:name="mark" android:scaleX="0.62" android:scaleY="0.62" android:pivotX="{CX:.0f}" android:pivotY="{CY:.0f}">
    <group android:name="hooks" android:pivotX="{CX:.0f}" android:pivotY="{CY:.0f}">
{chr(10).join(hooks)}
    </group>
    <path android:name="body" android:pathData="{p_circle(CX, CY, R)}">
      <aapt:attr name="android:fillColor">
        <gradient android:type="radial" android:centerX="{CX - 70:.0f}" android:centerY="{CY - 110:.0f}" android:gradientRadius="{R * 1.6:.0f}">
          <item android:offset="0" android:color="{BODY_LIGHT}"/>
          <item android:offset="0.55" android:color="{BODY}"/>
          <item android:offset="1" android:color="{BODY_SHADE}"/>
        </gradient>
      </aapt:attr>
    </path>
    <group android:name="eyes" android:pivotX="{CX:.0f}" android:pivotY="{ey:.0f}">
{chr(10).join(eyes)}
    </group>
  </group>
</vector>
"""


def animated_vector() -> str:
    """Android 12+ splash: the hooks wobble, the mark bobs once, the eyes blink. 1000 ms total (windowSplashScreenAnimationDuration)."""
    return """<?xml version="1.0" encoding="utf-8"?>
<!-- GENERATED by design/mascot/build_brand_assets.py. Do not edit by hand. -->
<animated-vector xmlns:android="http://schemas.android.com/apk/res/android"
    xmlns:aapt="http://schemas.android.com/aapt"
    android:drawable="@drawable/burdock_splash_icon">
  <target android:name="hooks">
    <aapt:attr name="android:animation">
      <set android:ordering="sequentially">
        <objectAnimator android:propertyName="rotation" android:valueFrom="0" android:valueTo="12" android:duration="260" android:interpolator="@android:interpolator/fast_out_slow_in"/>
        <objectAnimator android:propertyName="rotation" android:valueFrom="12" android:valueTo="-8" android:duration="300" android:interpolator="@android:interpolator/fast_out_slow_in"/>
        <objectAnimator android:propertyName="rotation" android:valueFrom="-8" android:valueTo="0" android:duration="340" android:interpolator="@android:interpolator/overshoot"/>
      </set>
    </aapt:attr>
  </target>
  <target android:name="mark">
    <aapt:attr name="android:animation">
      <set android:ordering="together">
        <objectAnimator android:propertyName="translateY" android:valueFrom="60" android:valueTo="0" android:duration="500" android:interpolator="@android:interpolator/overshoot"/>
        <objectAnimator android:propertyName="scaleX" android:valueFrom="0.5" android:valueTo="0.62" android:duration="500" android:interpolator="@android:interpolator/overshoot"/>
        <objectAnimator android:propertyName="scaleY" android:valueFrom="0.5" android:valueTo="0.62" android:duration="500" android:interpolator="@android:interpolator/overshoot"/>
      </set>
    </aapt:attr>
  </target>
  <target android:name="eyes">
    <aapt:attr name="android:animation">
      <set android:ordering="sequentially">
        <objectAnimator android:propertyName="scaleY" android:valueFrom="1" android:valueTo="1" android:duration="600"/>
        <objectAnimator android:propertyName="scaleY" android:valueFrom="1" android:valueTo="0.08" android:duration="90"/>
        <objectAnimator android:propertyName="scaleY" android:valueFrom="0.08" android:valueTo="1" android:duration="120"/>
      </set>
    </aapt:attr>
  </target>
</animated-vector>
"""


def main() -> int:
    targets = {
        PRESENTATION / "Resources" / "AppIcon" / "appicon.svg": background_svg(),
        PRESENTATION / "Resources" / "AppIcon" / "appiconfg.svg": mark_svg(with_background=False),
        PRESENTATION / "Resources" / "Splash" / "splash.svg": mark_svg(with_background=False),
        PRESENTATION / "Resources" / "Images" / "brand_mark.svg": mark_svg(with_background=False, tight=True),
        PRESENTATION / "Platforms" / "Android" / "Resources" / "drawable" / "burdock_splash_icon.xml": vector_drawable(),
        PRESENTATION / "Platforms" / "Android" / "Resources" / "drawable-v31" / "burdock_splash_anim.xml": animated_vector(),
        HERE / "src" / "brand_mark.svg": mark_svg(with_background=True),
    }
    for path, content in targets.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8", newline="\n")
        print("wrote", path.relative_to(REPO).as_posix())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
